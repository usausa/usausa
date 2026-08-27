using System.Globalization;
using System.Text;

namespace StatsGenerator.Cards;

internal static class ActivityCard
{
    private const int Width = 400;
    private const int Height = 174;
    private const int PadX = 20;
    private const int PlotLeft = 42;
    private const int PlotTop = 44;
    private const int PlotBottom = 140;
    private const int Days = 30;

    private static readonly int[] Steps = [5, 10, 20, 25, 50, 100, 200, 250, 500, 1000, 2000];

    public static string Render(ProfileStat profile)
    {
        var values = profile.Calendar.Recent(Days);
        var svg = new SvgBuilder(Width, Height, $"Contribution activity in the last {values.Length} days");
        svg.Text(PadX, 24, 14, "tt", $"Contribution activity — last {values.Length} days", weight: 500);

        if (values.Length < 2)
        {
            return svg.Build();
        }

        var peak = values.Max();
        var step = Steps.FirstOrDefault(x => peak <= x * 3, Steps[^1]);
        var top = Math.Max(step, (int)Math.Ceiling(peak / (double)step) * step);

        for (var value = step; value <= top; value += step)
        {
            var y = Scale(value, top);
            svg.Line(PlotLeft, y, Width - PadX, y, "gl");
            svg.Text(PlotLeft - 4, y + 4, 11, "tm n", SvgBuilder.Number(value), SvgBuilder.AnchorEnd);
        }

        svg.Line(PlotLeft, PlotBottom + 0.5d, Width - PadX, PlotBottom + 0.5d, "ax");

        var pitch = (Width - PlotLeft - PadX) / (double)(values.Length - 1);
        var points = values.Select((value, index) => (X: PlotLeft + (index * pitch), Y: Scale(value, top))).ToArray();

        var line = new StringBuilder();
        for (var i = 0; i < points.Length; i++)
        {
            line.Append(CultureInfo.InvariantCulture, $"{(i == 0 ? 'M' : 'L')}{SvgBuilder.N(points[i].X)},{SvgBuilder.N(points[i].Y)}");
            if (i < points.Length - 1)
            {
                line.Append(' ');
            }
        }

        svg.Path($"{line} L{SvgBuilder.N(Width - PadX)},{PlotBottom} L{PlotLeft},{PlotBottom} Z", "area", "opacity=\"0.12\"");
        svg.Path(line.ToString(), "acs", "fill=\"none\" stroke-width=\"2\" stroke-linejoin=\"round\" stroke-linecap=\"round\"");

        var peakIndex = Array.IndexOf(values, peak);
        svg.Text(points[peakIndex].X, points[peakIndex].Y - 8, 11, "tp n", SvgBuilder.Number(peak), SvgBuilder.AnchorMiddle);
        svg.Circle(points[peakIndex].X, points[peakIndex].Y, 4, "dot", strokeWidth: 2);

        var last = profile.Calendar.LastDay;
        var first = last.AddDays(-(values.Length - 1));
        svg.Text(PlotLeft, 158, 11, "tm n", $"{first.Month}/{first.Day}");
        svg.Text(Width - PadX, 158, 11, "tm n", $"{last.Month}/{last.Day}", SvgBuilder.AnchorEnd);

        return svg.Build();
    }

    private static double Scale(int value, int top) => PlotBottom - ((PlotBottom - PlotTop) * value / (double)top);
}
