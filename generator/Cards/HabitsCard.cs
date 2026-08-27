using System.Globalization;

namespace StatsGenerator.Cards;

internal static class HabitsCard
{
    private const int Width = (RepositoryCard.Width * 2) + 4;
    private const int Height = 178;
    private const int PadX = 20;
    private const int GridX = 54;
    private const int GridY = 42;
    private const int Cell = 16;
    private const int CellSize = 14;
    private const int DividerX = 480;
    private const int PanelX = 508;
    private const int PanelColumn = 142;

    private static readonly string[] DayNames = ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"];

    public static string Render(HabitStat habits, int offsetHours)
    {
        var zone = offsetHours == 0 ? "UTC" : $"UTC{offsetHours:+#;-#}";
        var svg = new SvgBuilder(Width, Height, $"Commit habits by hour of day ({zone})");
        svg.Text(PadX, 24, 14, "tt", $"Commit habits - by hour ({zone})", weight: 500);

        Legend(svg);

        for (var day = 0; day < 7; day++)
        {
            var y = GridY + (day * Cell);
            svg.Text(GridX - 6, y + 11, 10, "tm", DayNames[day], SvgBuilder.AnchorEnd);

            for (var hour = 0; hour < 24; hour++)
            {
                svg.Rect(GridX + (hour * Cell), y, CellSize, CellSize, $"h{Level(habits.Grid[day][hour], habits.Peak)}", 3);
            }
        }

        foreach (var hour in new[] { 0, 3, 6, 9, 12, 15, 18, 21 })
        {
            svg.Text(GridX + (hour * Cell) + (CellSize / 2.0d), 168, 10, "tm n", hour.ToString("00", CultureInfo.InvariantCulture), SvgBuilder.AnchorMiddle);
        }

        svg.Line(DividerX, 40, DividerX, 156, "ax");

        var (first, last) = habits.ActiveHours;
        var stats = new (string Label, string Value, bool Highlight)[]
        {
            ("peak hour", $"{habits.PeakHour:00}:00", true),
            ("busiest day", DayNames[habits.BusiestDay], false),
            ("commits", SvgBuilder.Number(habits.Total), false),
            ("active hours", $"{first:00}-{last:00}", false)
        };

        for (var i = 0; i < stats.Length; i++)
        {
            var x = PanelX + (i % 2 * PanelColumn);
            var y = 80 + (i / 2 * 52);
            svg.Text(x, y, 19, stats[i].Highlight ? "act n" : "tp n", stats[i].Value, weight: 500);
            svg.Text(x, y + 15, 10, "tm", stats[i].Label);
        }

        return svg.Build();
    }

    private static void Legend(SvgBuilder svg)
    {
        var right = (double)(Width - PadX);
        svg.Text(right, 24, 10, "tm", "More", SvgBuilder.AnchorEnd);
        right -= 28;

        for (var i = 4; i >= 0; i--)
        {
            right -= 12;
            svg.Rect(right, 14, 10, 10, $"h{i}", 2);
        }

        svg.Text(right - 6, 24, 10, "tm", "Less", SvgBuilder.AnchorEnd);
    }

    // Relative to the busiest cell, so the contrast holds whatever the commit volume is.
    private static int Level(int value, int peak)
    {
        if ((value == 0) || (peak == 0))
        {
            return 0;
        }

        var ratio = value / (double)peak;
        return ratio switch
        {
            <= 0.25d => 1,
            <= 0.50d => 2,
            <= 0.75d => 3,
            _ => 4
        };
    }
}
