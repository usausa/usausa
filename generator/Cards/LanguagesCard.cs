using System.Globalization;

namespace StatsGenerator.Cards;

internal static class LanguagesCard
{
    private const int Width = 400;
    private const int Height = 152;
    private const int PadX = 20;
    private const int BarY = 50;
    private const int BarHeight = 10;
    private const int Gap = 2;
    private const int MinSegment = 3;
    private const int TopCount = 5;
    private const int FirstLegend = 76;
    private const int LegendPitch = 24;
    private const int LegendColumn = 190;

    public static string Render(ProfileStat profile)
    {
        var svg = new SvgBuilder(Width, Height, "Most used languages");
        svg.Text(PadX, 24, 14, "tt", "Most used languages", weight: 500);
        svg.Text(PadX, 40, 11, "tm", $"by code size · {profile.RepositoryCount} public repos");

        var entries = Aggregate(profile.Languages);
        if (entries.Length == 0)
        {
            return svg.Build();
        }

        var barWidth = Width - (PadX * 2);
        var widths = Distribute(entries, barWidth - (Gap * (entries.Length - 1)));

        svg.Rect(PadX, BarY, barWidth, BarHeight, "h0", BarHeight / 2.0d);

        var x = (double)PadX;
        for (var i = 0; i < entries.Length; i++)
        {
            svg.Rect(x, BarY, widths[i], BarHeight, null, fill: entries[i].Color);
            x += widths[i] + Gap;
        }

        for (var i = 0; i < entries.Length; i++)
        {
            var legendX = PadX + (i % 2 * LegendColumn);
            var legendY = FirstLegend + (i / 2 * LegendPitch);
            var percent = entries[i].Percent.ToString("0.0", CultureInfo.InvariantCulture) + "%";

            svg.Rect(legendX, legendY, 10, 10, null, 2, entries[i].Color);
            svg.Text(legendX + 16, legendY + 9, 12, "tp", entries[i].Name);
            svg.Text(legendX + 22 + TextMeasure.Width(entries[i].Name, 12), legendY + 9, 12, "tm n", percent);
        }

        return svg.Build();
    }

    private static (string Name, string Color, double Percent)[] Aggregate(LanguageStat[] languages)
    {
        var total = languages.Sum(static x => x.Size);
        if (total == 0)
        {
            return [];
        }

        var top = languages.Take(TopCount).ToArray();
        var entries = top
            .Select(x => (x.Name, x.Color, Percent: x.Size * 100.0d / total))
            .ToList();

        var rest = total - top.Sum(static x => x.Size);
        if (rest > 0)
        {
            entries.Add(("Other", "#8b949e", rest * 100.0d / total));
        }

        return [.. entries];
    }

    private static int[] Distribute((string Name, string Color, double Percent)[] entries, int available)
    {
        var widths = entries.Select(x => Math.Max(MinSegment, (int)Math.Round(x.Percent / 100.0d * available))).ToArray();

        // Rounding and the minimum segment width can push the total past the bar, so trim from the widest segment.
        var difference = widths.Sum() - available;
        while (difference > 0)
        {
            widths[Array.IndexOf(widths, widths.Max())]--;
            difference--;
        }

        if (difference < 0)
        {
            widths[0] -= difference;
        }

        return widths;
    }
}
