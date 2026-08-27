using System.Net.Http.Headers;
using System.Text.Json;

namespace StatsGenerator;

internal sealed class NuGetClient : IDisposable
{
    private const string ServiceIndex = "https://api.nuget.org/v3/index.json";
    private const int PageSize = 200;

    private readonly HttpClient client;

    public NuGetClient()
    {
        client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("usausa-stats-generator", "1.0"));
        client.Timeout = TimeSpan.FromSeconds(60);
    }

    public void Dispose() => client.Dispose();

    // The search API cannot filter by owner, so query each package id prefix and keep the hits this owner published.
    public async Task<NuGetStat> GetStatAsync(NuGetSettings settings, int topCount)
    {
        var search = await GetSearchUrlAsync();
        var packages = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

        foreach (var prefix in settings.Prefixes)
        {
            var skip = 0;
            while (true)
            {
                var url = $"{search}?q={Uri.EscapeDataString(prefix)}&take={PageSize}&skip={skip}&prerelease=true&semVerLevel=2.0.0";
                using var document = await GetJsonAsync(url);

                var data = document.RootElement.GetProperty("data");
                var count = data.GetArrayLength();
                foreach (var package in data.EnumerateArray())
                {
                    if (!package.TryGetProperty("owners", out var owners) ||
                        !owners.EnumerateArray().Any(x => String.Equals(x.GetString(), settings.Owner, StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }

                    packages[package.GetProperty("id").GetString()!] = package.GetProperty("totalDownloads").GetInt64();
                }

                skip += count;
                if ((count < PageSize) || (skip >= document.RootElement.GetProperty("totalHits").GetInt32()))
                {
                    break;
                }
            }
        }

        var top = packages
            .Select(static x => new PackageStat(x.Key, x.Value))
            .OrderByDescending(static x => x.Downloads)
            .Take(topCount)
            .ToArray();

        return new NuGetStat(packages.Count, packages.Values.Sum(), top);
    }

    private async Task<string> GetSearchUrlAsync()
    {
        using var document = await GetJsonAsync(ServiceIndex);

        var resources = document.RootElement.GetProperty("resources").EnumerateArray().ToArray();
        var resource = resources.FirstOrDefault(static x => x.GetProperty("@type").GetString() == "SearchQueryService");
        if (resource.ValueKind == JsonValueKind.Undefined)
        {
            resource = resources.First(static x => x.GetProperty("@type").GetString()?.StartsWith("SearchQueryService", StringComparison.Ordinal) == true);
        }

        return resource.GetProperty("@id").GetString()!;
    }

    private async Task<JsonDocument> GetJsonAsync(string url)
    {
        using var response = await client.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
    }
}
