namespace StatsGenerator.Cards;

internal sealed record Palette(
    string CardFill,
    string CardStroke,
    string Title,
    string Text,
    string Muted,
    string Grid,
    string Axis);

// The hue every card shares: the heatmap ramp, the activity line, and the download bars.
internal sealed record Accent(
    string[] LightRamp,
    string LightLine,
    string LightNumber,
    string[] DarkRamp,
    string DarkLine,
    string DarkNumber);

// Cards ship as a single file that adapts to the reader's color scheme. Light is the default so that a
// viewer which ignores the media query still renders the card the way it always looked.
internal static class Theme
{
    private const string FontStack = "-apple-system,BlinkMacSystemFont,'Segoe UI',Ubuntu,'Helvetica Neue',Arial,sans-serif";

    private static readonly Palette Light = new(
        CardFill: "#ffffff",
        CardStroke: "#d0d7de",
        Title: "#0969da",
        Text: "#1f2328",
        Muted: "#57606a",
        Grid: "#eaeef2",
        Axis: "#d0d7de");

    private static readonly Palette Dark = new(
        CardFill: "#0d1117",
        CardStroke: "#30363d",
        Title: "#58a6ff",
        Text: "#c9d1d9",
        Muted: "#8b949e",
        Grid: "#21262d",
        Axis: "#30363d");

    private static readonly Dictionary<string, Accent> Accents = new(StringComparer.OrdinalIgnoreCase)
    {
        ["green"] = new(
            ["#ebedf0", "#9be9a8", "#40c463", "#30a14e", "#216e39"], "#2da44e", "#1a7f37",
            ["#161b22", "#0e4429", "#006d32", "#26a641", "#39d353"], "#39d353", "#39d353"),
        ["blue"] = new(
            ["#ebedf0", "#b6e3ff", "#54aeff", "#0969da", "#0550ae"], "#0969da", "#0550ae",
            ["#161b22", "#0d419d", "#1158c7", "#388bfd", "#79c0ff"], "#58a6ff", "#58a6ff"),
        ["purple"] = new(
            ["#ebedf0", "#ecd8ff", "#c297ff", "#8250df", "#6639ba"], "#8250df", "#6639ba",
            ["#161b22", "#3c1e70", "#5e40a2", "#8957e5", "#bc8cff"], "#a371f7", "#a371f7"),
        ["orange"] = new(
            ["#ebedf0", "#ffd8b5", "#ffb77c", "#e16f24", "#bc4c00"], "#bc4c00", "#953800",
            ["#161b22", "#762d0a", "#bd561d", "#db6d28", "#f0883e"], "#f0883e", "#f0883e"),
        ["teal"] = new(
            ["#ebedf0", "#b3ece7", "#6dd3c9", "#189e93", "#0d6b64"], "#189e93", "#0d6b64",
            ["#161b22", "#0b4f4a", "#10726a", "#1f9c92", "#39d0c3"], "#39d0c3", "#39d0c3"),
        ["pink"] = new(
            ["#ebedf0", "#ffd7e8", "#ff9bce", "#bf3989", "#99286e"], "#bf3989", "#99286e",
            ["#161b22", "#5e103e", "#9e3670", "#db61a2", "#f778ba"], "#db61a2", "#db61a2")
    };

    // Chosen once from settings before the first card is rendered, because every card embeds the palette.
    private static Accent accent = Accents["green"];

    public static void UseAccent(string name)
    {
        if (!Accents.TryGetValue(name, out var selected))
        {
            throw new ArgumentException($"Unknown accent '{name}'. Available: {String.Join(", ", Accents.Keys)}.", nameof(name));
        }

        accent = selected;
    }

    public static string Css() =>
        $"text{{font-family:{FontStack}}}" +
        ".n{font-variant-numeric:tabular-nums}" +
        Rules(Light, accent.LightRamp, accent.LightLine, accent.LightNumber) +
        $"@media(prefers-color-scheme:dark){{{Rules(Dark, accent.DarkRamp, accent.DarkLine, accent.DarkNumber)}}}";

    private static string Rules(Palette palette, string[] ramp, string line, string number) =>
        $".card{{fill:{palette.CardFill};stroke:{palette.CardStroke}}}" +
        $".tt{{fill:{palette.Title}}}" +
        $".tp{{fill:{palette.Text}}}" +
        $".tm{{fill:{palette.Muted}}}" +
        $".gl{{stroke:{palette.Grid}}}" +
        $".ax{{stroke:{palette.Axis}}}" +
        $".ac{{fill:{line}}}" +
        $".acs{{stroke:{line}}}" +
        $".act{{fill:{number}}}" +
        $".bar{{fill:{line}}}" +
        $".dot{{fill:{line};stroke:{palette.CardFill}}}" +
        $".area{{fill:{line}}}" +
        String.Concat(ramp.Select(static (color, index) => $".h{index}{{fill:{color}}}"));
}
