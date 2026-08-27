namespace StatsGenerator;

internal sealed record Settings(
    string User,
    NuGetSettings NuGet,
    CategorySettings[] Categories)
{
    public IEnumerable<RepositorySettings> AllRepositories => Categories.SelectMany(static x => x.Repositories);
}

internal sealed record NuGetSettings(string Owner, string[] Prefixes);

internal sealed record CategorySettings(string Title, RepositorySettings[] Repositories);

internal sealed record RepositorySettings(string Name, string Label);

internal sealed record LanguageStat(string Name, string Color, long Size);

internal sealed record RepositoryStat(
    string Name,
    string? Description,
    string? LanguageName,
    string? LanguageColor,
    int Stars,
    int Forks);

internal sealed record PackageStat(string Id, long Downloads);

internal sealed record NuGetStat(int PackageCount, long TotalDownloads, PackageStat[] Top);

internal sealed record CalendarStat(DateOnly FirstDay, int[] Days)
{
    public DateOnly LastDay => FirstDay.AddDays(Days.Length - 1);

    public int Total => Days.Sum();

    public int BestDay => Days.Length == 0 ? 0 : Days.Max();

    // A zero on the final day means the day is still in progress, so it does not break the streak.
    public int CurrentStreak
    {
        get
        {
            var index = Days.Length - 1;
            if ((index >= 0) && (Days[index] == 0))
            {
                index--;
            }

            var streak = 0;
            while ((index >= 0) && (Days[index] > 0))
            {
                streak++;
                index--;
            }

            return streak;
        }
    }

    public int[] Recent(int count) => Days.Length <= count ? Days : Days[^count..];
}

internal sealed record ProfileStat(
    string User,
    int Followers,
    int Commits,
    int TotalStars,
    int TotalForks,
    int RepositoryCount,
    CalendarStat Calendar,
    LanguageStat[] Languages,
    IReadOnlyDictionary<string, RepositoryStat> Repositories);
