using System.Text.Json;

using StatsGenerator;
using StatsGenerator.Cards;

var output = ArgumentOf(args, "--output") ?? "dist";
var settingsPath = ArgumentOf(args, "--settings") ?? Path.Combine(AppContext.BaseDirectory, "settings.json");

var token = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
if (String.IsNullOrEmpty(token))
{
    Console.Error.WriteLine("GITHUB_TOKEN is required.");
    return 1;
}

var settings = JsonSerializer.Deserialize<Settings>(
    File.ReadAllText(settingsPath),
    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

Console.WriteLine($"Fetching GitHub statistics for {settings.User}...");
using var github = new GitHubClient(token);
var profile = await github.GetProfileAsync(settings.User);
Console.WriteLine($"  {profile.RepositoryCount} repos · {profile.TotalStars} stars · {profile.Calendar.Total} contributions · {profile.Languages.Length} languages");

Console.WriteLine("Fetching NuGet statistics...");
using var nuget = new NuGetClient();
var packages = await nuget.GetStatAsync(settings.NuGet, 4);
Console.WriteLine($"  {packages.PackageCount} packages · {packages.TotalDownloads:N0} downloads");

var repositoryDirectory = Path.Combine(output, "repo");
Directory.CreateDirectory(repositoryDirectory);

var summary = new Dictionary<string, string>
{
    ["overview.svg"] = OverviewCard.Render(profile),
    ["languages.svg"] = LanguagesCard.Render(profile),
    ["contributions.svg"] = ContributionsCard.Render(profile),
    ["activity.svg"] = ActivityCard.Render(profile),
    ["nuget.svg"] = NuGetCard.Render(packages)
};

foreach (var (name, content) in summary)
{
    File.WriteAllText(Path.Combine(output, name), content);
}

var written = 0;
foreach (var entry in settings.AllRepositories)
{
    if (!profile.Repositories.TryGetValue(entry.Name, out var repository))
    {
        Console.Error.WriteLine($"  warning: {entry.Name} is not in the public repository list, skipped");
        continue;
    }

    File.WriteAllText(Path.Combine(repositoryDirectory, $"{entry.Name}.svg"), RepositoryCard.Render(repository));
    written++;
}

var emoji = await EmojiResolver.LoadAsync(token);
File.WriteAllText(Path.Combine(output, "index.html"), IndexPage.Render(settings, emoji, [.. summary.Keys], DateTimeOffset.UtcNow));
File.WriteAllText(Path.Combine(output, ".nojekyll"), String.Empty);

Console.WriteLine($"Wrote {summary.Count} summary cards and {written} repository cards to {Path.GetFullPath(output)}");
return 0;

static string? ArgumentOf(string[] args, string name)
{
    var index = Array.IndexOf(args, name);
    return (index >= 0) && (index + 1 < args.Length) ? args[index + 1] : null;
}
