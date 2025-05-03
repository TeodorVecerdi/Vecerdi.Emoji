using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using NeoSmart.Unicode;
using Vecerdi.Emoji.Text;
using Vecerdi.Logging;
using Range = System.Range;
using NeoEmoji = NeoSmart.Unicode.Emoji;

namespace Vecerdi.Emoji {
    public static class EmojiProcessor {
        /// <summary>
        /// Replaces all emojis in a string with sprite tags
        /// </summary>
        [return: NotNullIfNotNull("input")]
        public static string? ProcessEmojis(string? input, Func<string, string?> processEmoji) {
            if (string.IsNullOrEmpty(input))
                return input;

            var result = new StringBuilder();
            var sequence = input.AsUnicodeSequence();

            // Process the sequence grapheme by grapheme
            var i = 0;
            var codepoints = sequence.Codepoints.ToArray().AsSpan();
            while (i < codepoints.Length) {
                var (codepointRange, isEmoji) = ExtractGrapheme(codepoints, ref i);
                if (isEmoji) {
                    var emoji = CodepointsToString(codepoints[codepointRange]);
                    if (NeoEmoji.IsEmoji(emoji)) {
                        var processedEmoji = processEmoji(emoji);
                        if (processedEmoji is not null)
                            result.Append(processedEmoji);
                        continue;
                    }
                }

                result.Append(CodepointsToString(codepoints[codepointRange]));
            }

            return result.ToString();
        }

        /// <summary>
        /// Applies rule-based fallback to complex emoji sequences
        /// </summary>
        public static string[] GetFallbackEmojis(string? emoji, out string? contextFormat) {
            // TODO: We are allocating arrays when most of the paths return an empty array or a single element array.
            contextFormat = null;
            if (string.IsNullOrEmpty(emoji))
                return Array.Empty<string>();

            var codepoints = emoji.Codepoints().ToList();
            if (codepoints.Count == 0)
                return Array.Empty<string>();

            // RULE 1: Family emoji sequences (contain multiple people connected by ZWJ)
            if (codepoints.Contains(Codepoints.ZWJ) && IsFamilyEmoji(codepoints)) {
                // Individual people
                contextFormat = "<f:{0}>";
                var zwjCount = codepoints.Count(cp => cp == Codepoints.ZWJ);

                var result = new string[zwjCount + 1];
                var valueStringBuilder = new ValueStringBuilder(stackalloc char[1024]);
                var resultIndex = 0;
                foreach (var codepoint in codepoints) {
                    if (codepoint == Codepoints.ZWJ) {
                        result[resultIndex++] = valueStringBuilder.AsSpan().ToString();
                        valueStringBuilder.Length = 0;
                        continue;
                    }

                    valueStringBuilder.Append(codepoint.AsString());
                }

                result[resultIndex] = valueStringBuilder.AsSpan().ToString();
                valueStringBuilder.Dispose();

                return result;
            }

            // RULE 2: Professional/occupation emojis
            if (codepoints.Contains(Codepoints.ZWJ) && HasProfessionIndicator(codepoints)) {
                // TODO: Preserve person emoji modifiers (e.g., skin tone)
                contextFormat = "<p:{0}>";
                // Find the person emoji in the sequence (typically the first one)
                var personCodepoint = codepoints.FirstOrDefault(IsGenderSpecific);
                if (personCodepoint.Value == 0) {
                    personCodepoint = 0x1F9D1;
                }

                // Find the profession emoji
                var professionCodepoint = codepoints.FirstOrDefault(IsProfessionIndicator);
                if (professionCodepoint.Value != 0) {
                    return new[] { personCodepoint.AsString(), professionCodepoint.AsString() };
                }

                // If no profession emoji is found, return the person emoji
                return new[] { personCodepoint.AsString() };
            }

            // RULE 3: Remove skin tone modifiers
            if (codepoints.Any(cp => NeoEmoji.SkinTones.All.Contains(cp))) {
                return new[] {
                    string.Concat(codepoints
                        .Where(cp => !NeoEmoji.SkinTones.All.Contains(cp))
                        .Select(cp => char.ConvertFromUtf32((int)cp.Value))),
                };
            }

            // RULE 4: Gender-specific to gender-neutral
            if (codepoints.Count == 1 && IsGenderSpecific(codepoints[0])) {
                return new[] { char.ConvertFromUtf32(GetGenderNeutral(codepoints[0])) };
            }

            // RULE 5: If the sequence starts with a flag (`🏳`) - could fall back to a generic flag or globe
            if ((codepoints.Count > 0 && codepoints[0].Value is 0x1F3F3) || IsFlag(codepoints)) {
                Log.Debug($"Emoji sequence starts with flag, falling back to globe: {string.Concat(codepoints.Select(cp => cp.AsString()))}", EmojiLogging.Category);
                return new[] { "🌐" }; // 🌐 Globe with meridians
            }

            // RULE 6: For ZWJ sequences not handled above, take the first emoji
            if (codepoints.Contains(Codepoints.ZWJ)) {
                Log.Debug($"Emoji sequence contains ZWJ, taking first emoji. First codepoint: {codepoints[0].Value},{codepoints[0].Value:x},{codepoints[0].AsString()}, sequence: {string.Concat(codepoints.Select(cp => cp.AsString()))}", EmojiLogging.Category);
                var firstEmojiEnd = codepoints.IndexOf(Codepoints.ZWJ);
                if (firstEmojiEnd > 0) {
                    // Return just the first part before ZWJ
                    return new[] { string.Concat(codepoints.Take(firstEmojiEnd).Select(cp => cp.AsString())) };
                }
            }

            // If no rules apply, return the original emoji
            return new[] { emoji };
        }

        /// <summary>
        /// Extracts a complete emoji sequence (grapheme) from a list of codepoints, starting at the given index.
        /// Updates the index to point to the first codepoint after the extracted sequence.
        /// </summary>
        /// <param name="codepoints">The list of codepoints to extract from</param>
        /// <param name="index">The starting index, will be updated to point after the sequence</param>
        /// <returns>A tuple containing the range of codepoints and a flag indicating if it's an emoji</returns>
        private static (Range Range, bool IsEmoji) ExtractGrapheme(ReadOnlySpan<Codepoint> codepoints, ref int index) {
            if (index >= codepoints.Length) {
                return (new Range(index, index), false);
            }

            var currentCp = codepoints[index];

            // Check for Regional Indicator Symbols (country flags)
            // Regional Indicator Symbols range from U+1F1E6 to U+1F1FF
            var isRegionalIndicator = currentCp.Value is >= 0x1F1E6 and <= 0x1F1FF;
            if (isRegionalIndicator && index + 1 < codepoints.Length) {
                var nextCp = codepoints[index + 1];
                if (nextCp.Value is >= 0x1F1E6 and <= 0x1F1FF) {
                    // We have a pair of regional indicators - this is a country flag
                    index += 2; // Skip both symbols
                    return (new Range(index - 2, index), true);
                }
            }

            // Check if this could be an emoji
            var isEmoji = currentCp.Value >= 0x200D && Languages.Emoji.Contains(currentCp);
            if (!isEmoji) {
                index++;
                return (new Range(index - 1, index), false);
            }

            var needsVS = Languages.ArabicNumerals.Contains(currentCp) || currentCp.Value is 0x23 or 0x2A;
            if (needsVS && (index + 1 >= codepoints.Length || !IsVariationSelector(codepoints[index + 1]))) {
                // If we need a variation selector but there's no one, this isn't an emoji
                index++;
                return (new Range(index - 1, index), false);
            }

            var startIndex = index;
            index++;

            var foundZwj = false;

            // Collect all components of the emoji sequence
            while (index < codepoints.Length) {
                var cp = codepoints[index];
                if (cp == NeoEmoji.VariationSelector) {
                    index++;
                } else if (NeoEmoji.SkinTones.All.Contains(cp)) {
                    index++;
                } else if (cp == NeoEmoji.ZeroWidthJoiner) {
                    foundZwj = true;
                    index++;
                } else if (cp == NeoEmoji.Keycap) {
                    index++;
                } else if (foundZwj && Languages.Emoji.Contains(cp)) {
                    // An emoji after a ZWJ - add it and continue building the sequence
                    foundZwj = false;
                    index++;
                } else {
                    // Not part of the emoji sequence
                    break;
                }
            }

            return (new Range(startIndex, index), true);
        }

        private static bool IsVariationSelector(Codepoint cp) {
            // U+FE0x: VS1 through VS16
            return cp.Value is >= 0xFE00 and <= 0xFE0F;
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

        private static string CodepointsToString(ReadOnlySpan<Codepoint> codepoints) {
            if (codepoints.IsEmpty)
                return string.Empty;

            // Estimate required capacity (1 or 2 chars per codepoint)
            var estimatedCapacity = codepoints.Length * 2;
            const int stackallocThreshold = 2048;
            using var sb = estimatedCapacity <= stackallocThreshold
                ? new ValueStringBuilder(stackalloc char[estimatedCapacity])
                : new ValueStringBuilder(estimatedCapacity);

            Span<ushort> utf16Chars = stackalloc ushort[2];
            for (var i = 0; i < codepoints.Length; i++) {
                var count = codepoints[i].AsUtf16(utf16Chars);
                var cast = MemoryMarshal.Cast<ushort, char>(utf16Chars[..count]);
                sb.Append(cast);
            }

            return sb.AsSpan().ToString();
        }
    }
}
