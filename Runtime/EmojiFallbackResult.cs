using System;
using System.Collections.Generic;

namespace Vecerdi.Emoji;

/// <summary>
/// Represents the result of an emoji fallback operation
/// </summary>
public readonly struct EmojiFallbackResult {
    /// <summary>
    /// Fallback emojis that should be used instead of the original
    /// </summary>
    public IReadOnlyList<string>? Emojis { get; }

    /// <summary>
    /// Represents a single fallback emoji, or null if no single emoji is available.
    /// </summary>
    public string? SingleEmoji { get; }

    /// <summary>
    /// The fallback rule that was applied
    /// </summary>
    public EmojiFallbackRule Rule { get; }

    /// <summary>
    /// Whether the fallback operation found any replacement emojis
    /// </summary>
    public bool HasFallback => SingleEmoji is not null || Emojis?.Count > 0;

    /// <summary>
    /// Creates a fallback result with multiple emojis and the applied rule
    /// </summary>
    public EmojiFallbackResult(IReadOnlyList<string> emojis, EmojiFallbackRule rule) {
        Emojis = emojis;
        SingleEmoji = null;
        Rule = rule;
    }

    /// <summary>
    /// Creates a fallback result with a single emoji and the applied rule
    /// </summary>
    public EmojiFallbackResult(string emoji, EmojiFallbackRule rule) {
        Emojis = null;
        SingleEmoji = emoji;
        Rule = rule;
    }

    /// <summary>
    /// Creates an empty fallback result (no replacement found)
    /// </summary>
    public static EmojiFallbackResult Empty { get; } = new(Array.Empty<string>(), EmojiFallbackRule.None);
}
