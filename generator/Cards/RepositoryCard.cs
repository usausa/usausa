namespace StatsGenerator.Cards;

internal static class RepositoryCard
{
    public const int Width = 400;

    private const int Height = 72;
    private const int PadX = 20;
    private const int TitleX = 44;
    private const int TitleY = 24;
    private const int DescriptionY = 44;
    private const int FooterY = 60;
    private const int MetricGap = 14;

    private static readonly string[] Separators = [" - ", " \u2014 ", " \u2013 "];

    // starGain is the change over the last 30 days; it is shown next to the star count when positive.
    public static string Render(RepositoryStat repository, long starGain = 0)
    {
        var svg = new SvgBuilder(Width, Height, $"{repository.Name} repository stats");

        svg.Icon(Octicons.Repo, PadX, 12);
        svg.Text(TitleX, TitleY, 14, "tt", TextMeasure.Wrap(repository.Name, 14, Width - TitleX - PadX, 1)[0], weight: 500);

        var headline = Headline(repository.Description);
        if (headline.Length > 0)
        {
            svg.Text(PadX, DescriptionY, 12, "tm", TextMeasure.Wrap(headline, 12, Width - (PadX * 2), 1)[0]);
        }

        var x = (double)PadX;
        if (repository.LanguageName is not null)
        {
            svg.Circle(x + 5, FooterY - 4, 5, null, repository.LanguageColor ?? "#8b949e");
            svg.Text(x + 16, FooterY, 12, "tp", repository.LanguageName);
            x += 16 + TextMeasure.Width(repository.LanguageName, 12) + MetricGap;
        }

        x = Metric(svg, x, Octicons.Star, repository.Stars);
        if (starGain > 0)
        {
            var gain = $"+{SvgBuilder.Number(starGain)}";
            svg.Text(x - MetricGap + 4, FooterY, 11, "act n", gain);
            x += TextMeasure.Width(gain, 11) + 4;
        }

        Metric(svg, x, Octicons.Fork, repository.Forks);

        return svg.Build();
    }

    // Descriptions follow the "headline - details" convention; the card only has room for the headline.
    private static string Headline(string? description)
    {
        if (String.IsNullOrWhiteSpace(description))
        {
            return String.Empty;
        }

        foreach (var separator in Separators)
        {
            var index = description.IndexOf(separator, StringComparison.Ordinal);
            if (index > 0)
            {
                return description[..index].Trim();
            }
        }

        return description.Trim();
    }

    private static double Metric(SvgBuilder svg, double x, string icon, int value)
    {
        var text = SvgBuilder.Number(value);
        svg.Icon(icon, x, FooterY - 12, scale: 0.75);
        svg.Text(x + 17, FooterY, 12, "tp n", text);
        return x + 17 + TextMeasure.Width(text, 12) + MetricGap;
    }
}
