using System.Collections.Generic;
using System.Linq;
using Vecerdi.Logging;

namespace Vecerdi.Emoji {
    public sealed class EmojiProcessingManager {
        private readonly Dictionary<string, string> m_EmojiToSpriteIdCache = new();
        private readonly ISet<EmojiCode> m_AvailableEmojis;

        public EmojiProcessingManager(ISet<EmojiCode> availableEmojis) {
            m_AvailableEmojis = availableEmojis;
        }

        public string ProcessText(string text) {
            return EmojiProcessor.ProcessEmojis(text, ProcessEmoji);
        }

        private string? ProcessEmoji(string emoji) {
            var emojiCode = EmojiCode.FromLiteral(emoji);
            Log.Debug($"Processing emoji {emoji} ({emojiCode})", EmojiLogging.Category);

            if (m_AvailableEmojis.Contains(emojiCode)) {
                Log.Debug($"Final emoji {emoji} ({emojiCode})", EmojiLogging.Category);
                return GetSpriteTagForEmoji(emoji);
            }

            // Try to find an available fallback emoji
            var currentEmoji = "";
            while (emoji != currentEmoji && !m_AvailableEmojis.Contains(emojiCode)) {
                currentEmoji = emoji;

                var emojis = EmojiProcessor.GetFallbackEmojis(emoji, out var context).ToList();
                switch (emojis.Count) {
                    // Failed to find a fallback emoji
                    case 0: {
                        Log.Debug($"No fallback emoji found for {emoji} ({emojiCode})", EmojiLogging.Category);
                        return null;
                    }
                    // Found a single fallback emoji
                    case 1: {
                        emoji = emojis[0];
                        if (emoji == currentEmoji) {
                            Log.Debug($"No fallback emoji found for {emoji} ({emojiCode})", EmojiLogging.Category);
                            return null;
                        }

                        emojiCode = EmojiCode.FromLiteral(emoji);
                        continue;
                    }
                    // Found multiple fallback emojis
                    default: {
                        if (context is not null) {
                            Log.Debug($"Fallback emoji {emoji} ({emojiCode}): {string.Join(", ", emojis)}", EmojiLogging.Category);
                            return string.Format(context, string.Join("", emojis.Select(GetSpriteTagForEmoji)));
                        }

                        Log.Debug($"Fallback emoji {emoji} ({emojiCode}): {string.Join(", ", emojis)}", EmojiLogging.Category);
                        return string.Join("", emojis.Select(GetSpriteTagForEmoji));
                    }
                }
            }

            if (!m_AvailableEmojis.Contains(emojiCode) && emojiCode.Value.Contains('-')) {
                Log.Debug($"Removing emoji {emoji} ({emojiCode})", EmojiLogging.Category);
                return null;
            }

            Log.Debug($"Final emoji {emoji} ({emojiCode})", EmojiLogging.Category);
            return GetSpriteTagForEmoji(emoji);
        }

        /// <summary>
        /// Generates a sprite tag for an emoji
        /// </summary>
        private string GetSpriteTagForEmoji(string emoji) {
            if (emoji.Length == 1)
                return emoji;

            // Check cache first
            if (m_EmojiToSpriteIdCache.TryGetValue(emoji, out var cachedTag))
                return cachedTag;

            var idBase = EmojiCode.FromLiteral(emoji).Value;
            var isSimpleEmoji = !idBase.Contains('-');
            var spriteTag = isSimpleEmoji ? emoji : $"<sprite name=\"{idBase}\">";

            // Cache the result
            m_EmojiToSpriteIdCache[emoji] = spriteTag;
            return spriteTag;
        }
    }
}
