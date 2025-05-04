namespace Vecerdi.Emoji;

/// <summary>
/// Indicates which fallback rule was applied to an emoji
/// </summary>
public enum EmojiFallbackRule {
    /// <summary>
    /// No fallback rule was applied, original emoji was used
    /// </summary>
    None = 0,

    /// <summary>
    /// Family emoji was broken into individual people
    /// </summary>
    Family = 1,

    /// <summary>
    /// Professional emoji was broken into person and profession
    /// </summary>
    Profession = 2,

    /// <summary>
    /// Skin tone modifier was removed
    /// </summary>
    SkinTone = 3,

    /// <summary>
    /// Gender-specific emoji was converted to gender-neutral
    /// </summary>
    GenderNeutral = 4,

    /// <summary>
    /// Flag emoji was converted to globe
    /// </summary>
    Flag = 5,

    /// <summary>
    /// Complex ZWJ sequence was simplified to its first component
    /// </summary>
    ZwjSequence = 6
}
