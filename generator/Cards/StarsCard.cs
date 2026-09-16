using System.Globalization;
using System.Text;

namespace StatsGenerator.Cards;

// Total stars over the last year on the left, the repositories that gained the most on the right.
// The curve comes from the star history, which is rebuilt from stargazer dates the first time a
// repository is seen, so it reaches back further than the daily snapshots do.
internal static class StarsCard
{
    private const int Width = 400;
    private const int Height = 174;
    private const int PadX = 20;
    private const int PlotLeft = 42;
    private const int PlotRight = 228;
    private const int PlotTop = 44;
    private const int PlotBottom = 140;
    private const int ListX = 248;
    private const int ListFirstRow = 66;
    private const int ListPitch = 18;
    private const int ListRows = 4;
    private const int Days = 365;
    private const int Samples = 53;

    private static readonly int[] Steps = [1, 2, 5, 10, 20, 25, 50, 100, 200, 250, 500, 1000];

    public static string Render(History history, DateOnly today)
    {
        var svg = new SvgBuilder(Width, Height, "Stars over the last 12 months");
        svg.Text(PadX, 24, 14, "tt", "Stars - last 12 months", weight: 500);

        var start = today.AddDays(-(Days - 1));
        var values = Enumerable.Range(0, Samples)
            .Select(i => history.StarsAt(i == Samples - 1 ? today : start.AddDays(i * (Days - 1) / (Samples - 1))))
            .ToArray();

        var gain = values[^1] - values[0];
        svg.Text(Width - PadX, 24, 12, "act n", $"{(gain >= 0 ? "+" : "")}{SvgBuilder.Number(gain)}", SvgBuilder.AnchorEnd, 500);

        // The axis starts near the lowest value rather than at zero, so a year of slow growth is visible.
        var low = values.Min();
        var high = values.Max();
        var step = Steps.FirstOrDefault(x => (high - low) <= x * 4, Steps[^1]);
        var bottom = Math.Max(0, (long)Math.Floor(low / (double)step) * step);
        var top = Math.Max(bottom + step, (long)Math.Ceiling(high / (double)step) * step);
        if (top == high)
        {
            top += step;
        }

        for (var value = bottom; value <= top; value += step)
        {
            var y = Scale(value, bottom, top);
            svg.Line(PlotLeft, y, PlotRight, y, "gl");
            svg.Text(PlotLeft - 4, y + 4, 11, "tm n", SvgBuilder.Number(value), SvgBuilder.AnchorEnd);
        }

        svg.Line(PlotLeft, PlotBottom + 0.5d, PlotRight, PlotBottom + 0.5d, "ax");

        var pitch = (PlotRight - PlotLeft) / (double)(Samples - 1);
        var points = values.Select((value, index) => (X: PlotLeft + (index * pitch), Y: Scale(value, bottom, top))).ToArray();

        var line = new StringBuilder();
        for (var i = 0; i < points.Length; i++)
        {
            line.Append(CultureInfo.InvariantCulture, $"{(i == 0 ? 'M' : 'L')}{SvgBuilder.N(points[i].X)},{SvgBuilder.N(points[i].Y)}");
            if (i < points.Length - 1)
            {
                line.Append(' ');
            }
        }

        svg.Path($"{line} L{PlotRight},{PlotBottom} L{PlotLeft},{PlotBottom} Z", "area", "opacity=\"0.12\"");
        svg.Path(line.ToString(), "acs", "fill=\"none\" stroke-width=\"2\" stroke-linejoin=\"round\" stroke-linecap=\"round\"");
        svg.Text(points[^1].X, points[^1].Y - 8, 11, "tp n", SvgBuilder.Number(values[^1]), SvgBuilder.AnchorMiddle);
        svg.Circle(points[^1].X, points[^1].Y, 4, "dot", strokeWidth: 2);

        svg.Text(PlotLeft, 158, 11, "tm n", $"{start.Year}/{start.Month}");
        svg.Text(PlotRight, 158, 11, "tm n", $"{today.Year}/{today.Month}", SvgBuilder.AnchorEnd);

        svg.Text(ListX, 48, 11, "tm", "Most starred this year");
        var gainers = history.Repositories.Keys
            .Select(name => (Name: name, Gain: history.StarGain(name, today, Days)))
            .Where(static x => x.Gain > 0)
            .OrderByDescending(static x => x.Gain)
            .ThenBy(static x => x.Name, StringComparer.OrdinalIgnoreCase)
            .Take(ListRows)
            .ToArray();

        if (gainers.Length == 0)
        {
            svg.Text(ListX, ListFirstRow, 11, "tm", "No new stars");
            return svg.Build();
        }

        for (var i = 0; i < gainers.Length; i++)
        {
            var y = ListFirstRow + (i * ListPitch);
            var label = $"+{SvgBuilder.Number(gainers[i].Gain)}";
            var nameWidth = Width - PadX - ListX - TextMeasure.Width(label, 11) - 8;
            svg.Text(ListX, y, 11, "tp", TextMeasure.Wrap(gainers[i].Name, 11, nameWidth, 1)[0]);
            svg.Text(Width - PadX, y, 11, "act n", label, SvgBuilder.AnchorEnd);
        }

        return svg.Build();
    }

    private static double Scale(long value, long bottom, long top) => PlotBottom - ((PlotBottom - PlotTop) * (value - bottom) / (double)(top - bottom));
}
