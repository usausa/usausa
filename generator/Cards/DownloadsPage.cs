using System.Globalization;
using System.Text;

namespace StatsGenerator.Cards;

// The page behind the download trend card: which packages the daily gain came from. Markdown rather than
// SVG because it is a table to read, and GitHub renders it on the gh-pages branch.
internal static class DownloadsPage
{
    private const int LeaderboardDays = 30;
    private const int LeaderboardRows = 20;
    private const int RecentDays = 14;
    private const int DailyRows = 10;

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

        markdown.Append(CultureInfo.InvariantCulture, $"Gains are the difference between the daily snapshots kept since {first:yyyy-MM-dd}. The totals come from the profile page, the per-package numbers from the NuGet search index, which is rebuilt on its own schedule: on a day it did not refresh no package moves, and the next refresh carries the days in between.{Environment.NewLine}{Environment.NewLine}");

        Leaderboard(markdown, history, today, first.Value);
        ByDay(markdown, history, today, first.Value);

        return markdown.ToString();
    }

    private static void Leaderboard(StringBuilder markdown, History history, DateOnly today, DateOnly first)
    {
        // Before the first snapshot there is nothing to diff against, and the whole lifetime total would
        // read as a gain.
        var from = Max(today.AddDays(-LeaderboardDays), first);
        var rows = history.Packages
            .Select(x => (Id: x.Key, Gain: History.ValueAt(x.Value, today) - History.BaselineAt(x.Value, from), Total: History.ValueAt(x.Value, today)))
            .Where(static x => x.Gain > 0)
            .OrderByDescending(static x => x.Gain)
            .ThenBy(static x => x.Id, StringComparer.OrdinalIgnoreCase)
            .Take(LeaderboardRows)
            .ToArray();

        markdown.Append(CultureInfo.InvariantCulture, $"## Since {from:yyyy-MM-dd}{Environment.NewLine}{Environment.NewLine}");
        if (rows.Length == 0)
        {
            markdown.Append(CultureInfo.InvariantCulture, $"No downloads were recorded.{Environment.NewLine}{Environment.NewLine}");
            return;
        }

        markdown.Append(CultureInfo.InvariantCulture, $"| # | Package | Gain | Total |{Environment.NewLine}| ---: | --- | ---: | ---: |{Environment.NewLine}");
        for (var i = 0; i < rows.Length; i++)
        {
            markdown.Append(CultureInfo.InvariantCulture, $"| {i + 1} | [{rows[i].Id}](https://www.nuget.org/packages/{rows[i].Id}) | +{rows[i].Gain:N0} | {rows[i].Total:N0} |{Environment.NewLine}");
        }

        markdown.Append(Environment.NewLine);
    }

    private static DateOnly Max(DateOnly x, DateOnly y) => x > y ? x : y;

    private static void ByDay(StringBuilder markdown, History history, DateOnly today, DateOnly first)
    {
        markdown.Append(CultureInfo.InvariantCulture, $"## By day{Environment.NewLine}{Environment.NewLine}");

        var written = 0;
        for (var day = today; (day > first) && (written < RecentDays); day = day.AddDays(-1))
        {
            var previous = day.AddDays(-1);
            var total = (history.DownloadsAt(day) ?? 0) - (history.DownloadsAt(previous) ?? 0);
            var rows = history.Packages
                .Select(x => (Id: x.Key, Gain: History.ValueAt(x.Value, day) - History.BaselineAt(x.Value, previous)))
                .Where(static x => x.Gain > 0)
                .OrderByDescending(static x => x.Gain)
                .ThenBy(static x => x.Id, StringComparer.OrdinalIgnoreCase)
                .Take(DailyRows)
                .ToArray();

            markdown.Append(CultureInfo.InvariantCulture, $"### {day:yyyy-MM-dd} (+{Math.Max(0, total):N0}){Environment.NewLine}{Environment.NewLine}");
            if (rows.Length == 0)
            {
                markdown.Append(CultureInfo.InvariantCulture, $"The search index did not refresh on this day.{Environment.NewLine}{Environment.NewLine}");
            }
            else
            {
                markdown.Append(CultureInfo.InvariantCulture, $"| Package | Gain |{Environment.NewLine}| --- | ---: |{Environment.NewLine}");
                foreach (var row in rows)
                {
                    markdown.Append(CultureInfo.InvariantCulture, $"| [{row.Id}](https://www.nuget.org/packages/{row.Id}) | +{row.Gain:N0} |{Environment.NewLine}");
                }

                markdown.Append(Environment.NewLine);
            }

            written++;
        }
    }
}
