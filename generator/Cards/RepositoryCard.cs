namespace StatsGenerator.Cards;

internal static class RepositoryCard
{
    public const int Width = 400;

    private const int Height = 88;
    private const int PadX = 20;
    private const int TitleX = 44;
    private const int TitleY = 24;
    private const int DescriptionY = 44;
    private const int DescriptionPitch = 15;
    private const int FooterY = 75;
    private const int MetricGap = 14;

    public static string Render(RepositoryStat repository)
    {
        var svg = new SvgBuilder(Width, Height, $"{repository.Name} repository stats");

        svg.Icon(Octicons.Repo, PadX, 12);
        svg.Text(TitleX, TitleY, 14, "tt", TextMeasure.Wrap(repository.Name, 14, Width - TitleX - PadX, 1)[0], weight: 500);

        if (!String.IsNullOrWhiteSpace(repository.Description))
        {
            var lines = TextMeasure.Wrap(repository.Description, 12, Width - (PadX * 2), 2);
            for (var i = 0; i < lines.Length; i++)
            {
                svg.Text(PadX, DescriptionY + (i * DescriptionPitch), 12, "tm", lines[i]);
            }
        }

        var x = (double)PadX;
        if (repository.LanguageName is not null)
        {
            svg.Circle(x + 5, FooterY - 4, 5, null, repository.LanguageColor ?? "#8b949e");
            svg.Text(x + 16, FooterY, 12, "tp", repository.LanguageName);
            x += 16 + TextMeasure.Width(repository.LanguageName, 12) + MetricGap;
        }

        x = Metric(svg, x, Octicons.Star, repository.Stars);
        Metric(svg, x, Octicons.Fork, repository.Forks);

        return svg.Build();
    }

    private static double Metric(SvgBuilder svg, double x, string icon, int value)
    {
        var text = SvgBuilder.Number(value);
        svg.Icon(icon, x, FooterY - 12, scale: 0.75);
        svg.Text(x + 17, FooterY, 12, "tp n", text);
        return x + 17 + TextMeasure.Width(text, 12) + MetricGap;
    }
}
