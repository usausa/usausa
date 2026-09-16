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

    private const string CommitRepositoryQuery = """
        query($login: String!) {
          user(login: $login) {
            id
            contributionsCollection {
              commitContributionsByRepository(maxRepositories: 100) {
                repository { name owner { login } }
              }
            }
          }
        }
        """;

    private const string CommitHistoryQuery = """
        query($owner: String!, $name: String!, $author: ID!, $since: GitTimestamp!, $cursor: String) {
          repository(owner: $owner, name: $name) {
            defaultBranchRef {
              target {
                ... on Commit {
                  history(first: 100, after: $cursor, since: $since, author: { id: $author }) {
                    pageInfo { hasNextPage endCursor }
                    nodes { committedDate }
                  }
                }
              }
            }
          }
        }
        """;

    private const string StargazerQuery = """
        query($owner: String!, $name: String!, $cursor: String) {
          repository(owner: $owner, name: $name) {
            stargazers(first: 100, after: $cursor, orderBy: { field: STARRED_AT, direction: ASC }) {
              pageInfo { hasNextPage endCursor }
              edges { starredAt }
            }
          }
        }
        """;

    // A runaway guard: no repository of this profile comes close, and it bounds the daily run.
    private const int MaxHistoryPages = 30;

    private const string UnknownLanguageColor = "#8b949e";

    private readonly HttpClient client;

    private readonly Dictionary<string, string> languageColors;

    public GitHubClient(string token, IReadOnlyDictionary<string, string>? colorOverrides = null)
    {
        languageColors = new Dictionary<string, string>(colorOverrides ?? new Dictionary<string, string>(), StringComparer.OrdinalIgnoreCase);

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

    // The contribution calendar has no clock, so the commit times come from each repository the profile
    // committed to over the same window.
    public async Task<HabitStat> GetHabitsAsync(string login, int offsetHours)
    {
        string authorId;
        var repositories = new List<(string Owner, string Name)>();

        using (var list = await QueryAsync(CommitRepositoryQuery, new Dictionary<string, object?> { ["login"] = login }))
        {
            var user = list.RootElement.GetProperty("data").GetProperty("user");
            authorId = user.GetProperty("id").GetString()!;

            foreach (var entry in user.GetProperty("contributionsCollection").GetProperty("commitContributionsByRepository").EnumerateArray())
            {
                var repository = entry.GetProperty("repository");
                repositories.Add((repository.GetProperty("owner").GetProperty("login").GetString()!, repository.GetProperty("name").GetString()!));
            }
        }

        var grid = Enumerable.Range(0, 7).Select(static _ => new int[24]).ToArray();
        var offset = TimeSpan.FromHours(offsetHours);
        var since = DateTimeOffset.UtcNow.AddYears(-1).ToString("yyyy-MM-ddTHH:mm:ssZ");
        var total = 0;
        var skipped = 0;

        foreach (var (owner, name) in repositories)
        {
            string? cursor = null;
            var pages = 0;

            do
            {
                JsonDocument page;
                try
                {
                    page = await QueryAsync(CommitHistoryQuery, new Dictionary<string, object?>
                    {
                        ["owner"] = owner,
                        ["name"] = name,
                        ["author"] = authorId,
                        ["since"] = since,
                        ["cursor"] = cursor
                    });
                }
                catch (InvalidOperationException)
                {
                    // Private or otherwise invisible to this token; the shape of the histogram survives it.
                    skipped++;
                    break;
                }

                using (page)
                {
                    var repository = page.RootElement.GetProperty("data").GetProperty("repository");
                    var branch = repository.ValueKind == JsonValueKind.Null ? default : repository.GetProperty("defaultBranchRef");
                    if (branch.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
                    {
                        break;
                    }

                    var history = branch.GetProperty("target").GetProperty("history");
                    foreach (var node in history.GetProperty("nodes").EnumerateArray())
                    {
                        var at = DateTimeOffset.Parse(node.GetProperty("committedDate").GetString()!).ToOffset(offset);
                        grid[(int)at.DayOfWeek][at.Hour]++;
                        total++;
                    }

                    var info = history.GetProperty("pageInfo");
                    cursor = info.GetProperty("hasNextPage").GetBoolean() ? info.GetProperty("endCursor").GetString() : null;
                }

                pages++;
            }
            while ((cursor is not null) && (pages < MaxHistoryPages));
        }

        return new HabitStat(grid, total, skipped);
    }

    // When each current stargazer starred the repository, as dates in the configured zone. Stars that
    // were removed since are invisible here, so a curve rebuilt from this can sit slightly below what
    // daily snapshots would have recorded.
    public async Task<List<DateOnly>> GetStarDatesAsync(string owner, string name, int offsetHours)
    {
        var offset = TimeSpan.FromHours(offsetHours);
        var dates = new List<DateOnly>();
        string? cursor = null;

        try
        {
            do
            {
            using var page = await QueryAsync(StargazerQuery, new Dictionary<string, object?>
            {
                ["owner"] = owner,
                ["name"] = name,
                ["cursor"] = cursor
            });

                var stargazers = page.RootElement.GetProperty("data").GetProperty("repository").GetProperty("stargazers");
                foreach (var edge in stargazers.GetProperty("edges").EnumerateArray())
                {
                    var at = DateTimeOffset.Parse(edge.GetProperty("starredAt").GetString()!).ToOffset(offset);
                    dates.Add(DateOnly.FromDateTime(at.DateTime));
                }

                var info = stargazers.GetProperty("pageInfo");
                cursor = info.GetProperty("hasNextPage").GetBoolean() ? info.GetProperty("endCursor").GetString() : null;
            }
            while (cursor is not null);

            return dates;
        }
        catch (InvalidOperationException)
        {
            // The workflow token is an app installation, which GraphQL keeps away from stargazer lists.
            // The REST endpoint answers the same question with the star media type.
            return await GetStarDatesRestAsync(owner, name, offset);
        }
    }

    private async Task<List<DateOnly>> GetStarDatesRestAsync(string owner, string name, TimeSpan offset)
    {
        var dates = new List<DateOnly>();
        for (var pageNumber = 1; ; pageNumber++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{owner}/{name}/stargazers?per_page=100&page={pageNumber}");
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.star+json"));

            using var response = await client.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"REST stargazers {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
            }

            using var page = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
            var count = 0;
            foreach (var entry in page.RootElement.EnumerateArray())
            {
                var at = DateTimeOffset.Parse(entry.GetProperty("starred_at").GetString()!).ToOffset(offset);
                dates.Add(DateOnly.FromDateTime(at.DateTime));
                count++;
            }

            if (count < 100)
            {
                return dates;
            }
        }
    }

    // Linguist reassigns colors from time to time, so a language can be pinned in settings.
    private string ColorOf(string language, string? reported) =>
        languageColors.TryGetValue(language, out var pinned) ? pinned : reported ?? UnknownLanguageColor;

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

                var primary = language.ValueKind == JsonValueKind.Null ? null : language.GetProperty("name").GetString();

                repositories[name] = new RepositoryStat(
                    name,
                    node.GetProperty("description").GetString(),
                    primary,
                    primary is null ? null : ColorOf(primary, language.GetProperty("color").GetString()),
                    node.GetProperty("stargazerCount").GetInt32(),
                    node.GetProperty("forkCount").GetInt32());

                foreach (var edge in node.GetProperty("languages").GetProperty("edges").EnumerateArray())
                {
                    var languageName = edge.GetProperty("node").GetProperty("name").GetString()!;
                    var color = ColorOf(languageName, edge.GetProperty("node").GetProperty("color").GetString());
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
            var message = errors.ToString();
            document.Dispose();
            throw new InvalidOperationException($"GraphQL error: {message}");
        }

        return document;
    }
}
