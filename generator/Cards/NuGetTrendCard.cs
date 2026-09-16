using System.Globalization;
using System.Text;

namespace StatsGenerator.Cards;

// Downloads gained per day, from the snapshots in the history. The first day a snapshot exists has no
// previous value to diff against, so the curve starts one day after the history does.
internal static class NuGetTrendCard
{
    private const int Width = 400;
    private const int Height = 174;
    private const int PadX = 20;
    private const int PlotLeft = 42;
    private const int PlotTop = 44;
    private const int PlotBottom = 140;
    private const int Days = 30;

    private static readonly int[] Steps = [5, 10, 20, 25, 50, 100, 200, 250, 500, 1000, 2000, 2500, 5000, 10000, 20000, 25000, 50000, 100000];

    public static string Render(History history, DateOnly today)
    {
        var svg = new SvgBuilder(Width, Height, $"NuGet downloads per day in the last {Days} days");
        svg.Text(PadX, 24, 14, "tt", $"NuGet downloads - last {Days} days", weight: 500);
        svg.Line(PlotLeft, PlotBottom + 0.5d, Width - PadX, PlotBottom + 0.5d, "ax");

        var start = today.AddDays(-(Days - 1));
        var first = history.FirstSnapshot;
        var known = first is null ? today.AddDays(1) : Max(start, first.Value.AddDays(1));
        if (known > today)
        {
            var since = first ?? today;
            svg.Text((PlotLeft + Width - PadX) / 2d, (PlotTop + PlotBottom) / 2d, 12, "tm", $"Collecting daily data since {since.Month}/{since.Day}", SvgBuilder.AnchorMiddle);
            return svg.Build();
        }

        var offset = known.DayNumber - start.DayNumber;
        var count = Days - offset;
        var values = new long[Days];
        for (var i = 0; i < Days; i++)
        {
            var day = start.AddDays(i);
            if (day < known)
            {
                continue;
            }

            var current = history.DownloadsAt(day) ?? 0;
            var previous = history.DownloadsAt(day.AddDays(-1)) ?? current;
            values[i] = Math.Max(0, current - previous);
        }

        var peak = values.Max();
        var step = Steps.FirstOrDefault(x => peak <= x * 3, Steps[^1]);
        var top = Math.Max(step, (long)Math.Ceiling(peak / (double)step) * step);

        for (var value = step; value <= top; value += step)
        {
            var y = Scale(value, top);
            svg.Line(PlotLeft, y, Width - PadX, y, "gl");
            svg.Text(PlotLeft - 4, y + 4, 11, "tm n", SvgBuilder.Number(value), SvgBuilder.AnchorEnd);
        }

        var pitch = (Width - PlotLeft - PadX) / (double)(Days - 1);
        var points = Enumerable.Range(offset, count)
            .Select(i => (X: PlotLeft + (i * pitch), Y: Scale(values[i], top)))
            .ToArray();

        if (points.Length == 1)
        {
            svg.Circle(points[0].X, points[0].Y, 4, "dot", strokeWidth: 2);
        }
        else
        {
            var line = new StringBuilder();
            for (var i = 0; i < points.Length; i++)
            {
                line.Append(CultureInfo.InvariantCulture, $"{(i == 0 ? 'M' : 'L')}{SvgBuilder.N(points[i].X)},{SvgBuilder.N(points[i].Y)}");
                if (i < points.Length - 1)
                {
                    line.Append(' ');
                }
            }

            svg.Path($"{line} L{SvgBuilder.N(points[^1].X)},{PlotBottom} L{SvgBuilder.N(points[0].X)},{PlotBottom} Z", "area", "opacity=\"0.12\"");
            svg.Path(line.ToString(), "acs", "fill=\"none\" stroke-width=\"2\" stroke-linejoin=\"round\" stroke-linecap=\"round\"");
        }

        var peakIndex = Array.IndexOf(values, peak, offset);
        var peakPoint = points[peakIndex - offset];
        svg.Text(peakPoint.X, peakPoint.Y - 8, 11, "tp n", SvgBuilder.Number(peak), SvgBuilder.AnchorMiddle);
        svg.Circle(peakPoint.X, peakPoint.Y, 4, "dot", strokeWidth: 2);

        var total = values.Sum();
        svg.Text(Width - PadX, 24, 12, "act n", $"+{SvgBuilder.Number(total)}", SvgBuilder.AnchorEnd, 500);
        svg.Text(PlotLeft, 158, 11, "tm n", $"{known.Month}/{known.Day}");
        svg.Text(Width - PadX, 158, 11, "tm n", $"{today.Month}/{today.Day}", SvgBuilder.AnchorEnd);

        return svg.Build();
    }

    private static DateOnly Max(DateOnly x, DateOnly y) => x > y ? x : y;

    private static double Scale(long value, long top) => PlotBottom - ((PlotBottom - PlotTop) * value / (double)top);
}
