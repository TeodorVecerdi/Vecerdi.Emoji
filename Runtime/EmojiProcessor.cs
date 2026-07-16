using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using NeoSmart.Unicode;
using Vecerdi.Emoji.Text;
using Vecerdi.Logging;
using NeoEmoji = NeoSmart.Unicode.Emoji;

namespace Vecerdi.Emoji {
    public static class EmojiProcessor {
        private const char ZeroWidthJoiner = (char)0x200D;
        private const int VariationSelector16 = 0xFE0F;
        private const int CombiningKeycap = 0x20E3;
        private const int SkinToneFirst = 0x1F3FB; // NeoEmoji.SkinTones.All is exactly U+1F3FB..U+1F3FF
        private const int SkinToneLast = 0x1F3FF;
        private const int RegionalIndicatorFirst = 0x1F1E6;
        private const int RegionalIndicatorLast = 0x1F1FF;

        /// <summary>
        /// Replaces all emojis in a string with sprite tags
        /// </summary>
        /// <remarks>
        /// Returns the <paramref name="input"/> instance itself when nothing needs replacing — in particular for
        /// text containing no character that could start an emoji (no codepoint below U+200D can), which makes
        /// re-scanning streamed plain-text prefixes allocation-free. Unpaired surrogates pass through as text.
        /// </remarks>
        [return: NotNullIfNotNull("input")]
        public static string? ProcessEmojis(string? input, Func<string, string?> processEmoji) {
            if (string.IsNullOrEmpty(input))
                return input;

            var span = input.AsSpan();
            var result = default(ValueStringBuilder);
            var changed = false;
            var runStart = 0; // start of the pending verbatim slice of the input
            var i = 0;

            while (i < span.Length) {
                var c = span[i];
                if (c < ZeroWidthJoiner) {
                    i++;
                    continue;
                }

                int cp;
                int cpLength;
                if (char.IsHighSurrogate(c)) {
                    if (i + 1 >= span.Length || !char.IsLowSurrogate(span[i + 1])) {
                        i++;
                        continue;
                    }

                    cp = char.ConvertToUtf32(c, span[i + 1]);
                    cpLength = 2;
                } else if (char.IsLowSurrogate(c)) {
                    i++;
                    continue;
                } else {
                    cp = c;
                    cpLength = 1;
                }

                var graphemeStart = i;

                if (cp is >= RegionalIndicatorFirst and <= RegionalIndicatorLast && IsRegionalIndicator(span, i + cpLength)) {
                    // A pair of regional indicator symbols - a country flag (never extended further)
                    i += cpLength + 2;
                } else if (!EmojiRangeLookup.Contains((uint)cp)) {
                    i += cpLength;
                    continue;
                } else {
                    i += cpLength;
                    ConsumeEmojiSequence(span, ref i);
                }

                var emoji = input.Substring(graphemeStart, i - graphemeStart);
                if (!NeoEmoji.IsEmoji(emoji))
                    continue; // suspected grapheme turned out not to be an emoji - stays in the verbatim run

                var processedEmoji = processEmoji(emoji);
                if (processedEmoji == emoji)
                    continue; // replaced by itself - keep it in the verbatim run

                if (!changed) {
                    changed = true;
                    result = new ValueStringBuilder(input.Length + 32);
                }

                result.Append(span[runStart..graphemeStart]);
                if (processedEmoji is not null)
                    result.Append(processedEmoji);
                runStart = i;
            }

            if (!changed)
                return input;

            result.Append(span[runStart..]);
            return result.ToString();
        }

        /// <summary>
        /// Applies rule-based fallback to complex emoji sequences
        /// </summary>
        public static EmojiFallbackResult GetFallback(string? emoji) {
            if (string.IsNullOrEmpty(emoji))
                return EmojiFallbackResult.Empty;

            var codepoints = emoji.Codepoints().ToList();
            if (codepoints.Count == 0)
                return EmojiFallbackResult.Empty;

            // RULE 1: Family emoji sequences (contain multiple people connected by ZWJ)
            if (codepoints.Contains(Codepoints.ZWJ) && IsFamilyEmoji(codepoints)) {
                // Individual people
                var zwjCount = codepoints.Count(cp => cp == Codepoints.ZWJ);

                var result = new List<string>(zwjCount + 1);
                var valueStringBuilder = new ValueStringBuilder(stackalloc char[1024]);

                foreach (var codepoint in codepoints) {
                    if (codepoint == Codepoints.ZWJ) {
                        result.Add(valueStringBuilder.AsSpan().ToString());
                        valueStringBuilder.Length = 0;
                        continue;
                    }

                    valueStringBuilder.Append(codepoint.AsString());
                }

                result.Add(valueStringBuilder.AsSpan().ToString());
                valueStringBuilder.Dispose();

                return new EmojiFallbackResult(result, EmojiFallbackRule.Family);
            }

            // RULE 2: Professional/occupation emojis
            if (codepoints.Contains(Codepoints.ZWJ) && HasProfessionIndicator(codepoints)) {
                // TODO: Preserve person emoji modifiers (e.g., skin tone)

                // Find the person emoji in the sequence (typically the first one)
                var personCodepoint = codepoints.FirstOrDefault(IsGenderSpecific);
                if (personCodepoint.Value == 0) {
                    personCodepoint = 0x1F9D1;
                }

                // Find the profession emoji
                var professionCodepoint = codepoints.FirstOrDefault(IsProfessionIndicator);
                if (professionCodepoint.Value != 0) {
                    return new EmojiFallbackResult(
                        new[] { personCodepoint.AsString(), professionCodepoint.AsString() },
                        EmojiFallbackRule.Profession
                    );
                }

                // If no profession emoji is found, return the person emoji
                return new EmojiFallbackResult(personCodepoint.AsString(), EmojiFallbackRule.Profession);
            }

            // RULE 3: Remove skin tone modifiers
            if (codepoints.Any(cp => NeoEmoji.SkinTones.All.Contains(cp))) {
                var fallbackEmoji = string.Concat(codepoints
                    .Where(cp => !NeoEmoji.SkinTones.All.Contains(cp))
                    .Select(cp => char.ConvertFromUtf32((int)cp.Value)));

                return new EmojiFallbackResult(fallbackEmoji, EmojiFallbackRule.SkinTone);
            }

            // RULE 4: Gender-specific to gender-neutral
            if (codepoints.Count == 1 && IsGenderSpecific(codepoints[0])) {
                return new EmojiFallbackResult(
                    char.ConvertFromUtf32(GetGenderNeutral(codepoints[0])),
                    EmojiFallbackRule.GenderNeutral
                );
            }

            // RULE 5: If the sequence starts with a flag (`🏳`) - could fall back to a generic flag or globe
            if ((codepoints.Count > 0 && codepoints[0].Value is 0x1F3F3) || IsFlag(codepoints)) {
                Log.Debug($"Emoji sequence starts with flag, falling back to globe: {string.Concat(codepoints.Select(cp => cp.AsString()))}", EmojiLogging.Category);
                return new EmojiFallbackResult("🌐", EmojiFallbackRule.Flag); // 🌐 Globe with meridians
            }

            // RULE 6: For ZWJ sequences not handled above, take the first emoji
            if (codepoints.Contains(Codepoints.ZWJ)) {
                Log.Debug($"Emoji sequence contains ZWJ, taking first emoji. First codepoint: {codepoints[0].Value},{codepoints[0].Value:x},{codepoints[0].AsString()}, sequence: {string.Concat(codepoints.Select(cp => cp.AsString()))}", EmojiLogging.Category);
                var firstEmojiEnd = codepoints.IndexOf(Codepoints.ZWJ);
                if (firstEmojiEnd > 0) {
                    // Return just the first part before ZWJ
                    var firstPartEmoji = string.Concat(codepoints.Take(firstEmojiEnd).Select(cp => cp.AsString()));
                    return new EmojiFallbackResult(firstPartEmoji, EmojiFallbackRule.ZwjSequence);
                }
            }

            // If no rules apply, return an empty result
            return EmojiFallbackResult.Empty;
        }

        /// <summary>
        /// Legacy method for backward compatibility. Use GetFallback instead.
        /// </summary>
        [Obsolete("Use GetFallback instead")]
        public static string[] GetFallbackEmojis(string? emoji, out string? contextFormat) {
            var result = GetFallback(emoji);

            // Convert rule to the old format string for backward compatibility
            contextFormat = result.Rule switch {
                EmojiFallbackRule.Family => "<f:{0}>",
                EmojiFallbackRule.Profession => "<p:{0}>",
                _ => null,
            };

            if (!result.HasFallback)
                return Array.Empty<string>();

            return result.SingleEmoji is not null ? new[] { result.SingleEmoji } : result.Emojis!.ToArray();
        }

        /// <summary>
        /// Checks whether a regional indicator symbol (always a surrogate pair) starts at <paramref name="index"/>.
        /// </summary>
        private static bool IsRegionalIndicator(ReadOnlySpan<char> span, int index) {
            return index + 1 < span.Length
                && char.IsHighSurrogate(span[index]) && char.IsLowSurrogate(span[index + 1])
                && char.ConvertToUtf32(span[index], span[index + 1]) is >= RegionalIndicatorFirst and <= RegionalIndicatorLast;
        }

        /// <summary>
        /// Advances <paramref name="index"/> past the components extending an emoji sequence whose base codepoint
        /// was already consumed: variation selectors (VS16), skin-tone modifiers, keycaps, and ZWJ-joined emoji.
        /// </summary>
        private static void ConsumeEmojiSequence(ReadOnlySpan<char> span, ref int index) {
            var foundZwj = false;

            while (index < span.Length) {
                var c = span[index];
                int cp;
                int cpLength;
                if (char.IsHighSurrogate(c)) {
                    if (index + 1 >= span.Length || !char.IsLowSurrogate(span[index + 1]))
                        break;

                    cp = char.ConvertToUtf32(c, span[index + 1]);
                    cpLength = 2;
                } else if (char.IsLowSurrogate(c)) {
                    break;
                } else {
                    cp = c;
                    cpLength = 1;
                }

                if (cp is VariationSelector16 or CombiningKeycap or >= SkinToneFirst and <= SkinToneLast) {
                    // Modifier - always part of the sequence
                } else if (cp == ZeroWidthJoiner) {
                    foundZwj = true;
                } else if (foundZwj && EmojiRangeLookup.Contains((uint)cp)) {
                    // An emoji after a ZWJ - add it and continue building the sequence
                    foundZwj = false;
                } else {
                    // Not part of the emoji sequence
                    break;
                }

                index += cpLength;
            }
        }

        /// <summary>
        /// Constant-time membership test for <see cref="Languages.Emoji"/>. <c>MultiRange.Contains</c> is a LINQ
        /// scan over ~160 ranges that allocates an enumerator per call - far too hot for the per-codepoint scanning
        /// path - so the ranges are flattened once into a BMP bitmask (8 KB) plus sorted astral range arrays.
        /// </summary>
        private static class EmojiRangeLookup {
            private static readonly ulong[] s_BmpBits;
            private static readonly uint[] s_AstralBegins;
            private static readonly uint[] s_AstralEnds;

            static EmojiRangeLookup() {
                var bits = new ulong[0x10000 / 64];
                var astral = new List<(uint Begin, uint End)>();

                foreach (var range in Languages.Emoji.Ranges) {
                    var begin = range.Begin.Value;
                    var end = range.End.Value;
                    if (begin <= 0xFFFF) {
                        var bmpEnd = Math.Min(end, 0xFFFFu);
                        for (var cp = begin; cp <= bmpEnd; cp++) {
                            bits[cp >> 6] |= 1UL << (int)(cp & 63);
                        }
                    }

                    if (end > 0xFFFF) {
                        astral.Add((Math.Max(begin, 0x10000u), end));
                    }
                }

                astral.Sort(static (a, b) => a.Begin.CompareTo(b.Begin));

                s_BmpBits = bits;
                s_AstralBegins = new uint[astral.Count];
                s_AstralEnds = new uint[astral.Count];
                for (var i = 0; i < astral.Count; i++) {
                    (s_AstralBegins[i], s_AstralEnds[i]) = astral[i];
                }
            }

            public static bool Contains(uint codepoint) {
                if (codepoint <= 0xFFFF)
                    return (s_BmpBits[codepoint >> 6] & (1UL << (int)(codepoint & 63))) != 0;

                var index = Array.BinarySearch(s_AstralBegins, codepoint);
                if (index >= 0)
                    return true;

                index = ~index - 1; // the last range starting before the codepoint
                return index >= 0 && codepoint <= s_AstralEnds[index];
            }
        }

        /// <summary>
        /// Checks if this is a family emoji sequence
        /// </summary>
        private static bool IsFamilyEmoji(List<Codepoint> codepoints) {
            // Family emojis typically have multiple person emojis connected by ZWJ
            var personCount = codepoints.Count(cp =>
                cp == 0x1F468 // 👨 Man
             || cp == 0x1F469 // 👩 Woman
             || cp == 0x1F466 // 👦 Boy
             || cp == 0x1F467 // 👧 Girl
             || cp == 0x1F9D1 // 🧑 Person
             || cp == 0x1F9D2); // 🧒 Child

            var zwjCount = codepoints.Count(cp => cp == Codepoints.ZWJ);

            // Family typically has multiple people and ZWJs connecting them
            return personCount >= 2 && zwjCount >= 1;
        }

        /// <summary>
        /// Checks if this emoji has a profession indicator
        /// </summary>
        private static bool HasProfessionIndicator(List<Codepoint> codepoints) {
            return codepoints.Any(IsProfessionIndicator);
        }

        /// <summary>
        /// Checks if the codepoint represents a profession indicator.
        /// </summary>
        private static bool IsProfessionIndicator(Codepoint cp) {
            // Common profession indicators
            return cp == 0x1F393 || // 🎓 Graduation Cap
                   cp == 0x1F3EB || // 🏫 School
                   cp == 0x1F3ED || // 🏭 Factory
                   cp == 0x1F4BB || // 💻 Laptop
                   cp == 0x1F4BC || // 💼 Briefcase
                   cp == 0x1F527 || // 🔧 Wrench
                   cp == 0x1F52C || // 🔬 Microscope
                   cp == 0x1F680 || // 🚀 Rocket
                   cp == 0x1F692 || // 🚒 Fire Engine
                   cp == 0x1F9AF || // 🦯 White Cane
                   cp == 0x1F3A4 || // 🎤 Microphone
                   cp == 0x1F3A8 || // 🎨 Artist Palette
                   cp == 0x02695 || // ⚕️ Medical Symbol
                   cp == 0x1F4E1 || // 📡 Satellite Antenna
                   cp == 0x1F373 || // 🍳 Cooking
                   cp == 0x1F52E; // 🔮 Crystal Ball
        }

        /// <summary>
        /// Checks if a code point represents a gender-specific emoji
        /// </summary>
        private static bool IsGenderSpecific(Codepoint codePoint) {
            return
                codePoint == 0x1F468 || // 👨 Man
                codePoint == 0x1F469 || // 👩 Woman
                codePoint == 0x1F466 || // 👦 Boy
                codePoint == 0x1F467; // 👧 Girl
        }

        /// <summary>
        /// Gets the gender-neutral equivalent of a gender-specific emoji
        /// </summary>
        private static int GetGenderNeutral(Codepoint codePoint) {
            return codePoint.Value switch {
                0x1F468 or 0x1F469 // 👩 Woman or 👨 Man
                    => 0x1F9D1, // 🧑 Person

                0x1F466 or 0x1F467 // 👧 Girl or 👦 Boy
                    => 0x1F9D2, // 🧒 Child
                _ => 0x1F9D1, // 🧑 Person
            };
        }

        /// <summary>
        /// Checks if this is a flag emoji
        /// </summary>
        private static bool IsFlag(List<Codepoint> codepoints) {
            // Regional indicator symbols are in the range 0x1F1E6-0x1F1FF
            return codepoints.Count >= 2 && codepoints.All(cp => cp.Value is >= 0x1F1E6 and <= 0x1F1FF);
        }
    }
}
