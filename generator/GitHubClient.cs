using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace StatsGenerator;

internal sealed class GitHubClient : IDisposable
{
    private const string Endpoint = "https://api.github.com/graphql";

    private const string ProfileQuery = """
        query($login: String!) {
          user(login: $login) {
            followers { totalCount }
            contributionsCollection {
              totalCommitContributions
              contributionCalendar {
                weeks { contributionDays { date contributionCount } }
              }
            }
          }
        }
        """;

    private const string RepositoryQuery = """
        query($login: String!, $cursor: String) {
          user(login: $login) {
            repositories(first: 100, after: $cursor, isFork: false, privacy: PUBLIC, ownerAffiliations: OWNER) {
              pageInfo { hasNextPage endCursor }
              nodes {
                name
                description
                stargazerCount
                forkCount
                primaryLanguage { name color }
                languages(first: 10, orderBy: { field: SIZE, direction: DESC }) {
                  edges { size node { name color } }
                }
              }
            }
          }
        }
        """;

    private readonly HttpClient client;

    public GitHubClient(string token)
    {
        client = new HttpClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("usausa-stats-generator", "1.0"));
        client.Timeout = TimeSpan.FromSeconds(60);
    }

    public void Dispose() => client.Dispose();

    public async Task<ProfileStat> GetProfileAsync(string login)
    {
        using var profile = await QueryAsync(ProfileQuery, new Dictionary<string, object?> { ["login"] = login });

        var user = profile.RootElement.GetProperty("data").GetProperty("user");
        var followers = user.GetProperty("followers").GetProperty("totalCount").GetInt32();
        var contributions = user.GetProperty("contributionsCollection");
        var commits = contributions.GetProperty("totalCommitContributions").GetInt32();
        var calendar = ReadCalendar(contributions.GetProperty("contributionCalendar"));

        var (repositories, languages, stars, forks) = await GetRepositoriesAsync(login);

        return new ProfileStat(
            login,
            followers,
            commits,
            stars,
            forks,
            repositories.Count,
            calendar,
            languages,
            repositories);
    }

    private static CalendarStat ReadCalendar(JsonElement calendar)
    {
        var days = new List<int>();
        var first = default(DateOnly?);

        foreach (var week in calendar.GetProperty("weeks").EnumerateArray())
        {
            foreach (var day in week.GetProperty("contributionDays").EnumerateArray())
            {
                first ??= DateOnly.Parse(day.GetProperty("date").GetString()!);
                days.Add(day.GetProperty("contributionCount").GetInt32());
            }
        }

        return new CalendarStat(first ?? DateOnly.FromDateTime(DateTime.UtcNow), [.. days]);
    }

    private async Task<(Dictionary<string, RepositoryStat> Repositories, LanguageStat[] Languages, int Stars, int Forks)> GetRepositoriesAsync(string login)
    {
        var repositories = new Dictionary<string, RepositoryStat>(StringComparer.OrdinalIgnoreCase);
        var sizes = new Dictionary<string, (string Color, long Size)>(StringComparer.Ordinal);
        var stars = 0;
        var forks = 0;
        string? cursor = null;

        do
        {
            using var page = await QueryAsync(RepositoryQuery, new Dictionary<string, object?> { ["login"] = login, ["cursor"] = cursor });

            var connection = page.RootElement.GetProperty("data").GetProperty("user").GetProperty("repositories");
            foreach (var node in connection.GetProperty("nodes").EnumerateArray())
            {
                var name = node.GetProperty("name").GetString()!;
                var language = node.GetProperty("primaryLanguage");

                stars += node.GetProperty("stargazerCount").GetInt32();
                forks += node.GetProperty("forkCount").GetInt32();

                repositories[name] = new RepositoryStat(
                    name,
                    node.GetProperty("description").GetString(),
                    language.ValueKind == JsonValueKind.Null ? null : language.GetProperty("name").GetString(),
                    language.ValueKind == JsonValueKind.Null ? null : language.GetProperty("color").GetString(),
                    node.GetProperty("stargazerCount").GetInt32(),
                    node.GetProperty("forkCount").GetInt32());

                foreach (var edge in node.GetProperty("languages").GetProperty("edges").EnumerateArray())
                {
                    var languageName = edge.GetProperty("node").GetProperty("name").GetString()!;
                    var color = edge.GetProperty("node").GetProperty("color").GetString() ?? "#8b949e";
                    var size = edge.GetProperty("size").GetInt64();
                    sizes[languageName] = sizes.TryGetValue(languageName, out var current)
                        ? (current.Color, current.Size + size)
                        : (color, size);
                }
            }

            var info = connection.GetProperty("pageInfo");
            cursor = info.GetProperty("hasNextPage").GetBoolean() ? info.GetProperty("endCursor").GetString() : null;
        }
        while (cursor is not null);

        var languages = sizes
            .Select(static x => new LanguageStat(x.Key, x.Value.Color, x.Value.Size))
            .OrderByDescending(static x => x.Size)
            .ToArray();

        return (repositories, languages, stars, forks);
    }

    private async Task<JsonDocument> QueryAsync(string query, Dictionary<string, object?> variables)
    {
        using var response = await client.PostAsJsonAsync(Endpoint, new { query, variables });
        response.EnsureSuccessStatusCode();

        var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        if (document.RootElement.TryGetProperty("errors", out var errors))
        {
            document.Dispose();
            throw new InvalidOperationException($"GraphQL error: {errors}");
        }

        return document;
    }
}
