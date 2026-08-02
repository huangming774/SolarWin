using SkiaSharp;
using SolarWin.Models.Analytics;
using System.Runtime.InteropServices;

namespace SolarWin.Services;

/// <summary>Deterministic center-out spiral placement with measured text collision boxes.</summary>
public sealed class WordCloudLayoutService : IWordCloudLayoutService
{
    private static readonly string[] ColorKeys =
    [
        "AccentTextFillColorPrimaryBrush",
        "SystemFillColorSuccessBrush",
        "SystemFillColorCautionBrush",
        "SystemFillColorCriticalBrush",
        "TextFillColorPrimaryBrush",
    ];

    public IReadOnlyList<WordCloudItem> Layout(
        IReadOnlyList<WordFrequencyItem> words,
        double width,
        double height,
        CancellationToken cancellationToken = default)
    {
        if (words.Count == 0 || width < 80 || height < 60) return [];

        width = Math.Clamp(width, 240, 2400);
        height = Math.Clamp(height, 160, 1400);
        var maxWeight = words.Max(static x => x.Weight);
        var minWeight = words.Min(static x => x.Weight);
        var occupied = new List<LayoutRect>(words.Count);
        var result = new List<WordCloudItem>(words.Count);

        foreach (var word in words.OrderByDescending(static x => x.Count).ThenBy(static x => x.Word, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var normalized = maxWeight <= minWeight ? 1 : (word.Weight - minWeight) / (maxWeight - minWeight);
            var fontSize = 14 + normalized * 38;
            var hash = StableHash(word.Word);
            var rotation = hash % 7 == 0 ? 90d : 0d; // roughly 14%, deterministic
            var measured = Measure(word.Word, fontSize, rotation);
            var found = false;
            LayoutRect placed = default;

            for (var step = 0; step < 2200; step++)
            {
                if ((step & 63) == 0) cancellationToken.ThrowIfCancellationRequested();
                var angle = step * 0.37 + (hash % 360) * Math.PI / 180d;
                var radius = 1.8 * Math.Sqrt(step);
                var x = width / 2 + Math.Cos(angle) * radius - measured.Width / 2;
                var y = height / 2 + Math.Sin(angle) * radius - measured.Height / 2;
                var candidate = new LayoutRect(x, y, measured.Width, measured.Height);
                if (candidate.Left < 2 || candidate.Top < 2 || candidate.Right > width - 2 || candidate.Bottom > height - 2)
                {
                    continue;
                }

                if (occupied.All(existing => !candidate.Intersects(existing, 3)))
                {
                    placed = candidate;
                    found = true;
                    break;
                }
            }

            if (!found) continue;
            occupied.Add(placed);
            result.Add(new WordCloudItem
            {
                Word = word.Word,
                Count = word.Count,
                FontSize = fontSize,
                X = placed.Left,
                Y = placed.Top,
                Rotation = rotation,
                ColorKey = ColorKeys[(int)(hash % (uint)ColorKeys.Length)],
            });
        }

        return result;
    }

    private static (double Width, double Height) Measure(string text, double fontSize, double rotation)
    {
        using var paint = new SKPaint { IsAntialias = true };
        using var font = new SKFont { Size = (float)fontSize };
        var glyphs = MemoryMarshal.Cast<char, ushort>(text.AsSpan());
        var width = Math.Max(fontSize, font.MeasureText(glyphs, paint)) + 6;
        var height = fontSize * 1.35 + 4;
        return rotation == 90 ? (height, width) : (width, height);
    }

    private static uint StableHash(string value)
    {
        const uint offset = 2166136261;
        const uint prime = 16777619;
        var hash = offset;
        foreach (var c in value)
        {
            hash ^= c;
            hash *= prime;
        }

        return hash;
    }

    private readonly record struct LayoutRect(double Left, double Top, double Width, double Height)
    {
        public double Right => Left + Width;
        public double Bottom => Top + Height;

        public bool Intersects(LayoutRect other, double padding)
            => Left < other.Right + padding
               && Right + padding > other.Left
               && Top < other.Bottom + padding
               && Bottom + padding > other.Top;
    }
}
