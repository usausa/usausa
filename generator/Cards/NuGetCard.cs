namespace StatsGenerator.Cards;

internal static class NuGetCard
{
    private const int Width = 400;
    private const int Height = 174;
    private const int PadX = 20;
    private const int FirstRow = 102;
    private const int RowPitch = 19;
    private const int NameWidth = 165;
    private const int BarX = 195;
    private const int BarWidth = 110;

    public static string Render(NuGetStat stat)
    {
        var svg = new SvgBuilder(Width, Height, "NuGet package downloads");
        svg.Text(PadX, 24, 14, "tt", "NuGet packages", weight: 500);
        svg.Text(PadX, 60, 26, "tp n", SvgBuilder.Number(stat.TotalDownloads), weight: 500);
        svg.Text(PadX, 76, 11, "tm", $"total downloads · {stat.PackageCount} packages");

        if (stat.Top.Length == 0)
        {
            return svg.Build();
        }

        var peak = stat.Top[0].Downloads;
        for (var i = 0; i < stat.Top.Length; i++)
        {
            var y = FirstRow + (i * RowPitch);
            var name = TextMeasure.Wrap(stat.Top[i].Id, 11, NameWidth, 1)[0];

            svg.Text(PadX, y, 11, "tm", name);
            svg.Rect(BarX, y - 8, Math.Max(2, stat.Top[i].Downloads * (double)BarWidth / peak), 8, "bar", 4);
            svg.Text(Width - PadX, y, 11, "tp n", SvgBuilder.Number(stat.Top[i].Downloads), SvgBuilder.AnchorEnd);
        }

        return svg.Build();
    }
}
