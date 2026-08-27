namespace StatsGenerator.Cards;

internal static class OverviewCard
{
    private const int Width = 400;
    private const int Height = 152;
    private const int PadX = 20;
    private const int FirstRow = 50;
    private const int RowPitch = 22;

    public static string Render(ProfileStat profile)
    {
        var svg = new SvgBuilder(Width, Height, $"{profile.User}'s GitHub stats");
        svg.Text(PadX, 24, 14, "tt", $"{profile.User}'s GitHub stats", weight: 500);

        var rows = new (string Icon, string Label, long Value)[]
        {
            (Octicons.Star, "Total stars", profile.TotalStars),
            (Octicons.Fork, "Total forks", profile.TotalForks),
            (Octicons.Commit, "Commits (1y)", profile.Commits),
            (Octicons.Pulse, "Contributions (1y)", profile.Calendar.Total),
            (Octicons.Repo, "Public repos", profile.RepositoryCount)
        };

        for (var i = 0; i < rows.Length; i++)
        {
            var y = FirstRow + (i * RowPitch);
            svg.Icon(rows[i].Icon, PadX, y - 12);
            svg.Text(PadX + 24, y, 12, "tp", rows[i].Label);
            svg.Text(Width - PadX, y, 12, "tp n", SvgBuilder.Number(rows[i].Value), SvgBuilder.AnchorEnd, 500);
        }

        return svg.Build();
    }
}
