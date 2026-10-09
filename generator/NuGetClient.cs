using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace StatsGenerator;

internal sealed class NuGetClient : IDisposable
{
    private const string ServiceIndex = "https://api.nuget.org/v3/index.json";
    private const string ProfilePage = "https://www.nuget.org/profiles/";
    private const int PageSize = 200;

    // The two numbers the profile page prints above the package list.
    private static readonly Regex ProfileStatistic = new(
        """<div class="value">([0-9,]+)</div>\s*<div class="description">(Packages|Total downloads of packages)</div>""",
        RegexOptions.IgnoreCase);

    private readonly HttpClient client;

    public NuGetClient()
    {
        client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("usausa-stats-generator", "1.0"));
        client.Timeout = TimeSpan.FromSeconds(60);
    }

    public void Dispose() => client.Dispose();

    // The search service understands the same owner: syntax as the nuget.org search box, so one query
    // returns exactly the packages the profile page counts. The owner check on each hit is a guard
    // against the syntax being treated as free text.
    //
    // Every region the service index lists is asked, and the highest count each package reports wins: a
    // region can stop refreshing its download counts for days while the others keep moving, and the run
    // would otherwise report a frozen total depending on which region it happened to pick.
    public async Task<NuGetStat> GetStatAsync(NuGetSettings settings, int topCount)
    {
        var packages = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        var failures = new List<string>();

        foreach (var search in await GetSearchUrlsAsync())
        {
            try
            {
                await ReadOwnedPackagesAsync(search, settings.Owner, packages);
            }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException)
            {
                failures.Add($"{search}: {e.Message}");
            }
        }

        if (packages.Count == 0)
        {
            throw new InvalidOperationException($"No NuGet search endpoint answered. {String.Join("; ", failures)}");
        }

        foreach (var failure in failures)
        {
            Console.Error.WriteLine($"  warning: search endpoint skipped, {failure}");
        }

        var top = packages
            .Select(static x => new PackageStat(x.Key, x.Value))
            .OrderByDescending(static x => x.Downloads)
            .Take(topCount)
            .ToArray();

        // The search index is rebuilt on its own schedule and can sit on the same counts for days, so the
        // totals come from the profile page, which reads the gallery itself and is what the owner sees.
        // Per-package counts stay with the search index: it is the only API that reports them.
        var totals = await GetProfileTotalsAsync(settings.Owner);
        var count = packages.Count;
        var downloads = packages.Values.Sum();
        if (totals is var (profilePackages, profileDownloads) && (profileDownloads >= downloads))
        {
            count = Math.Max(count, profilePackages);
            downloads = profileDownloads;
        }
        else
        {
            Console.Error.WriteLine("  warning: profile page totals unavailable, falling back to the search index");
        }

        return new NuGetStat(count, downloads, top, packages);
    }

    private async Task<(int Packages, long Downloads)?> GetProfileTotalsAsync(string owner)
    {
        string page;
        try
        {
            using var response = await client.GetAsync($"{ProfilePage}{Uri.EscapeDataString(owner)}");
            response.EnsureSuccessStatusCode();
            page = await response.Content.ReadAsStringAsync();
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            return null;
        }

        var packages = default(int?);
        var downloads = default(long?);
        foreach (var match in ProfileStatistic.Matches(page).Cast<Match>())
        {
            var value = Int64.Parse(match.Groups[1].Value.Replace(",", String.Empty, StringComparison.Ordinal), CultureInfo.InvariantCulture);
            if (match.Groups[2].Value.StartsWith("Packages", StringComparison.OrdinalIgnoreCase))
            {
                packages = (int)value;
            }
            else
            {
                downloads = value;
            }
        }

        return (packages is not null) && (downloads is not null) ? (packages.Value, downloads.Value) : null;
    }

    private async Task ReadOwnedPackagesAsync(string search, string owner, Dictionary<string, long> packages)
    {
        var skip = 0;
        while (true)
        {
            var url = $"{search}?q={Uri.EscapeDataString($"owner:{owner}")}&take={PageSize}&skip={skip}&prerelease=true&semVerLevel=2.0.0";
            using var document = await GetJsonAsync(url);

            var data = document.RootElement.GetProperty("data");
            var count = data.GetArrayLength();
            foreach (var package in data.EnumerateArray())
            {
                if (!package.TryGetProperty("owners", out var owners) ||
                    !owners.EnumerateArray().Any(x => String.Equals(x.GetString(), owner, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                var id = package.GetProperty("id").GetString()!;
                var downloads = package.GetProperty("totalDownloads").GetInt64();
                packages[id] = packages.TryGetValue(id, out var current) ? Math.Max(current, downloads) : downloads;
            }

            skip += count;
            if ((count == 0) || (count < PageSize) || (skip >= document.RootElement.GetProperty("totalHits").GetInt32()))
            {
                break;
            }
        }
    }

    private async Task<string[]> GetSearchUrlsAsync()
    {
        using var document = await GetJsonAsync(ServiceIndex);

        return [.. document.RootElement
            .GetProperty("resources")
            .EnumerateArray()
            .Where(static x => x.GetProperty("@type").GetString()?.StartsWith("SearchQueryService", StringComparison.Ordinal) == true)
            .Select(static x => x.GetProperty("@id").GetString()!)
            .Distinct(StringComparer.OrdinalIgnoreCase)];
    }

    private async Task<JsonDocument> GetJsonAsync(string url)
    {
        using var response = await client.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
    }
}
