namespace StatsGenerator.Cards;

internal static class ContributionsCard
{
    // Two 400px cards plus the space the markdown renderer leaves between them, so the wide card lines
    // up with the two-up rows in the README instead of overhanging them.
    private const int Width = (RepositoryCard.Width * 2) + 4;
    private const int Height = 150;
    private const int PadX = 20;
    private const int GridX = 52;
    private const int GridY = 48;
    private const int Cell = 10;
    private const int CellSize = 8;
    private const int DividerX = 620;

    private static readonly string[] MonthNames = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];

    public static string Render(ProfileStat profile)
    {
        var calendar = profile.Calendar;
        var svg = new SvgBuilder(Width, Height, $"{calendar.Total} contributions in the last year");
        svg.Text(PadX, 24, 14, "tt", $"{SvgBuilder.Number(calendar.Total)} contributions in the last year", weight: 500);

        var weeks = (calendar.Days.Length + 6) / 7;
        var previousMonth = -1;
        for (var w = 0; w < weeks; w++)
        {
            var month = calendar.FirstDay.AddDays(w * 7).Month;
            if (month == previousMonth)
            {
                continue;
            }

            previousMonth = month;
            if ((w > 0) && (w < weeks - 2))
            {
                svg.Text(GridX + (w * Cell), 42, 11, "tm", MonthNames[month - 1]);
            }
        }

        svg.Text(GridX - 6, GridY + Cell + 7, 11, "tm", "Mon", SvgBuilder.AnchorEnd);
        svg.Text(GridX - 6, GridY + (Cell * 3) + 7, 11, "tm", "Wed", SvgBuilder.AnchorEnd);
        svg.Text(GridX - 6, GridY + (Cell * 5) + 7, 11, "tm", "Fri", SvgBuilder.AnchorEnd);

        for (var w = 0; w < weeks; w++)
        {
            for (var d = 0; d < 7; d++)
            {
                var index = (w * 7) + d;
                if (index >= calendar.Days.Length)
                {
                    break;
                }

                svg.Rect(GridX + (w * Cell), GridY + (d * Cell), CellSize, CellSize, $"h{Level(calendar.Days[index])}", 2);
            }
        }

        var gridRight = GridX + (weeks * Cell) - (Cell - CellSize);
        svg.Text(gridRight - 92, 135, 11, "tm", "Less", SvgBuilder.AnchorEnd);
        for (var i = 0; i < 5; i++)
        {
            svg.Rect(gridRight - 86 + (i * 12), 126, 10, 10, $"h{i}", 2);
        }

        svg.Text(gridRight - 20, 135, 11, "tm", "More");

        svg.Line(DividerX, 44, DividerX, 128, "ax");
        svg.Text(DividerX + 28, 78, 24, "act n", $"{calendar.CurrentStreak}", weight: 500);
        svg.Text(DividerX + 28, 93, 11, "tm", "day streak");
        svg.Text(DividerX + 28, 120, 24, "tp n", SvgBuilder.Number(calendar.BestDay), weight: 500);
        svg.Text(DividerX + 28, 135, 11, "tm", "best day");

        return svg.Build();
    }

    private static int Level(int value) => value switch
    {
        0 => 0,
        < 10 => 1,
        < 30 => 2,
        < 80 => 3,
        _ => 4
    };
}
