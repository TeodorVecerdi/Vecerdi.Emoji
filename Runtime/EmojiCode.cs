using System;
using System.Diagnostics;
using Vecerdi.Emoji.Text;

namespace Vecerdi.Emoji;

public readonly struct EmojiCode : IEquatable<EmojiCode> {
    public readonly string Value;

    public EmojiCode(string emojiCode) {
        Value = emojiCode ?? throw new ArgumentNullException(nameof(emojiCode));
    }

    public static EmojiCode FromLiteral(string emojiLiteral) {
        var estimatedCapacity = emojiLiteral.Length * 9; // Max 8 hex chars + 1 hyphen per codepoint
        const int stackallocThreshold = 2048;

        using var sb = estimatedCapacity <= stackallocThreshold
            ? new ValueStringBuilder(stackalloc char[estimatedCapacity])
            : new ValueStringBuilder(estimatedCapacity);

        Span<char> hexBuffer = stackalloc char[8];
        for (var i = 0; i < emojiLiteral.Length; i += char.IsSurrogatePair(emojiLiteral, i) ? 2 : 1) {
            var value = char.ConvertToUtf32(emojiLiteral, i);
            var success = value.TryFormat(hexBuffer, out var charsWritten, "x");
            Debug.Assert(success);

            sb.Append(hexBuffer[..charsWritten]);
            sb.Append('-');
        }

        return new EmojiCode(sb.AsSpan(0, sb.Length - 1).ToString());
    }

    public bool Equals(EmojiCode other) => Value == other.Value;
    public override bool Equals(object? obj) => obj is EmojiCode other && Equals(other);
    public override int GetHashCode() => Value.GetHashCode();
    public static bool operator ==(EmojiCode left, EmojiCode right) => left.Equals(right);
    public static bool operator !=(EmojiCode left, EmojiCode right) => !left.Equals(right);

    public override string ToString() => Value;
    public static implicit operator string(EmojiCode code) => code.Value;
}
