using System.Globalization;
using System.Text;

namespace StatsGenerator.Cards;

// The page behind the download trend card: which packages a day's downloads came from. Markdown rather
// than SVG because it is a table to read, and GitHub renders it on the gh-pages branch.
internal static class DownloadsPage
{
    private const int RecentDays = 7;
    private const int DailyRows = 30;

    public static string Render(History history, NuGetStat packages, DateOnly today, DateTimeOffset generated)
    {
        var markdown = new StringBuilder();
        markdown.Append(CultureInfo.InvariantCulture, $"# NuGet downloads{Environment.NewLine}{Environment.NewLine}");
        markdown.Append(CultureInfo.InvariantCulture, $"Generated {generated:yyyy-MM-dd HH:mm} UTC · {packages.PackageCount:N0} packages · {packages.TotalDownloads:N0} total downloads{Environment.NewLine}{Environment.NewLine}");

        var first = history.FirstSnapshot;
        if (first is null)
        {
            markdown.Append(CultureInfo.InvariantCulture, $"No snapshot has been taken yet.{Environment.NewLine}");
            return markdown.ToString();
        }

        var written = 0;
        for (var day = today; (day > first) && (written < RecentDays); day = day.AddDays(-1))
        {
            Day(markdown, history, day);
            written++;
        }

        return markdown.ToString();
    }

    private static void Day(StringBuilder markdown, History history, DateOnly day)
    {
        var previous = day.AddDays(-1);
        var total = (history.DownloadsAt(day) ?? 0) - (history.DownloadsAt(previous) ?? 0);
        var rows = history.Packages
            .Select(x => (Id: x.Key, Gain: History.ValueAt(x.Value, day) - History.BaselineAt(x.Value, previous)))
            .Where(static x => x.Gain > 0)
            .OrderByDescending(static x => x.Gain)
            .ThenBy(static x => x.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        markdown.Append(CultureInfo.InvariantCulture, $"## {day:yyyy-MM-dd} (+{Math.Max(0, total):N0}){Environment.NewLine}{Environment.NewLine}");
        if (rows.Length == 0)
        {
            markdown.Append(CultureInfo.InvariantCulture, $"No package moved on this day.{Environment.NewLine}{Environment.NewLine}");
            return;
        }

        markdown.Append(CultureInfo.InvariantCulture, $"| Package | Gain |{Environment.NewLine}| --- | ---: |{Environment.NewLine}");
        foreach (var row in rows.Take(DailyRows))
        {
            markdown.Append(CultureInfo.InvariantCulture, $"| [{row.Id}](https://www.nuget.org/packages/{row.Id}) | +{row.Gain:N0} |{Environment.NewLine}");
        }

        // Whatever is past the cut is folded into one row, so the column still adds up to the day.
        if (rows.Length > DailyRows)
        {
            var rest = rows.Skip(DailyRows).ToArray();
            markdown.Append(CultureInfo.InvariantCulture, $"| {rest.Length:N0} more packages | +{rest.Sum(static x => x.Gain):N0} |{Environment.NewLine}");
        }

        markdown.Append(Environment.NewLine);
    }
}
