using System.Text.Json;

namespace StatsGenerator;

internal readonly record struct Point(DateOnly Date, long Value);

internal sealed record TotalEntry(DateOnly Date, long Downloads, int Packages, long Stars);

// Daily snapshots carried from one run to the next through history.json on the gh-pages branch, which is
// what the trend cards are drawn from. A series stores a point only when the value changes, so a package
// nobody downloads costs one entry rather than one a day, and star curves stay tiny.
internal sealed class History
{
    // Package points older than this are folded into a single carry-over point; star points are kept
    // forever because there are so few of them.
    public const int DetailDays = 400;

    public List<TotalEntry> Totals { get; } = [];

    public Dictionary<string, List<Point>> Packages { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, List<Point>> Repositories { get; } = new(StringComparer.OrdinalIgnoreCase);

    public DateOnly? FirstSnapshot => Totals.Count == 0 ? null : Totals[0].Date;

    public static History Load(string? path)
    {
        var history = new History();
        if (String.IsNullOrEmpty(path) || !File.Exists(path) || (new FileInfo(path).Length == 0))
        {
            return history;
        }

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;

        if (root.TryGetProperty("totals", out var totals))
        {
            foreach (var entry in totals.EnumerateArray())
            {
                history.Totals.Add(new TotalEntry(
                    DateOnly.Parse(entry.GetProperty("date").GetString()!),
                    entry.GetProperty("downloads").GetInt64(),
                    entry.GetProperty("packages").GetInt32(),
                    entry.GetProperty("stars").GetInt64()));
            }
        }

        ReadSeries(root, "packages", history.Packages);
        ReadSeries(root, "repos", history.Repositories);
        history.Totals.Sort(static (x, y) => x.Date.CompareTo(y.Date));
        return history;
    }

    public void Save(string path)
    {
        using var stream = File.Create(path);
        using var writer = new Utf8JsonWriter(stream);

        writer.WriteStartObject();
        writer.WriteNumber("version", 1);

        writer.WriteStartArray("totals");
        foreach (var entry in Totals)
        {
            writer.WriteStartObject();
            writer.WriteString("date", entry.Date.ToString("yyyy-MM-dd"));
            writer.WriteNumber("downloads", entry.Downloads);
            writer.WriteNumber("packages", entry.Packages);
            writer.WriteNumber("stars", entry.Stars);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();

        WriteSeries(writer, "packages", Packages);
        WriteSeries(writer, "repos", Repositories);
        writer.WriteEndObject();
    }

    public void Record(DateOnly date, NuGetStat packages, IEnumerable<RepositoryStat> repositories)
    {
        foreach (var (id, downloads) in packages.All)
        {
            Append(SeriesOf(Packages, id), date, downloads);
        }

        var stars = 0L;
        foreach (var repository in repositories)
        {
            stars += repository.Stars;
            if ((repository.Stars > 0) || Repositories.ContainsKey(repository.Name))
            {
                Append(SeriesOf(Repositories, repository.Name), date, repository.Stars);
            }
        }

        Totals.RemoveAll(x => x.Date == date);
        Totals.Add(new TotalEntry(date, packages.TotalDownloads, packages.PackageCount, stars));
        Totals.Sort(static (x, y) => x.Date.CompareTo(y.Date));
    }

    // A repository seen for the first time gets its star curve rebuilt from when each current stargazer
    // starred it, so the star cards have a past on the very first run.
    public void SeedRepository(string name, IEnumerable<DateOnly> starDates)
    {
        if (Repositories.ContainsKey(name))
        {
            return;
        }

        var series = SeriesOf(Repositories, name);
        var count = 0L;
        foreach (var group in starDates.GroupBy(static x => x).OrderBy(static x => x.Key))
        {
            count += group.Count();
            series.Add(new Point(group.Key, count));
        }
    }

    public void Prune(DateOnly cutoff)
    {
        foreach (var series in Packages.Values)
        {
            var index = series.FindLastIndex(x => x.Date < cutoff);
            if (index > 0)
            {
                series.RemoveRange(0, index);
            }
        }
    }

    public static long ValueAt(IReadOnlyList<Point> series, DateOnly date)
    {
        var value = 0L;
        foreach (var point in series)
        {
            if (point.Date > date)
            {
                break;
            }

            value = point.Value;
        }

        return value;
    }

    public long StarsOf(string name, DateOnly date) => Repositories.TryGetValue(name, out var series) ? ValueAt(series, date) : 0;

    public long StarsAt(DateOnly date) => Repositories.Values.Sum(series => ValueAt(series, date));

    public long StarGain(string name, DateOnly date, int days) => StarsOf(name, date) - StarsOf(name, date.AddDays(-days));

    // Null until the first snapshot on or before the date; a day between snapshots reports the last one.
    public long? DownloadsAt(DateOnly date)
    {
        var value = default(long?);
        foreach (var entry in Totals)
        {
            if (entry.Date > date)
            {
                break;
            }

            value = entry.Downloads;
        }

        return value;
    }

    private static void Append(List<Point> series, DateOnly date, long value)
    {
        if ((series.Count > 0) && (series[^1].Date == date))
        {
            series[^1] = new Point(date, value);
        }
        else if ((series.Count == 0) || (series[^1].Value != value))
        {
            series.Add(new Point(date, value));
        }
    }

    private static List<Point> SeriesOf(Dictionary<string, List<Point>> map, string key)
    {
        if (!map.TryGetValue(key, out var series))
        {
            series = [];
            map[key] = series;
        }

        return series;
    }

    private static void ReadSeries(JsonElement root, string name, Dictionary<string, List<Point>> map)
    {
        if (!root.TryGetProperty(name, out var element))
        {
            return;
        }

        foreach (var property in element.EnumerateObject())
        {
            var series = SeriesOf(map, property.Name);
            foreach (var point in property.Value.EnumerateArray())
            {
                series.Add(new Point(DateOnly.Parse(point[0].GetString()!), point[1].GetInt64()));
            }

            series.Sort(static (x, y) => x.Date.CompareTo(y.Date));
        }
    }

    private static void WriteSeries(Utf8JsonWriter writer, string name, Dictionary<string, List<Point>> map)
    {
        writer.WriteStartObject(name);
        foreach (var (key, series) in map.OrderBy(static x => x.Key, StringComparer.Ordinal))
        {
            writer.WriteStartArray(key);
            foreach (var point in series)
            {
                writer.WriteStartArray();
                writer.WriteStringValue(point.Date.ToString("yyyy-MM-dd"));
                writer.WriteNumberValue(point.Value);
                writer.WriteEndArray();
            }

            writer.WriteEndArray();
        }

        writer.WriteEndObject();
    }
}
