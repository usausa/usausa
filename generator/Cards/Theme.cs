namespace StatsGenerator.Cards;

internal sealed record Palette(
    string CardFill,
    string CardStroke,
    string Title,
    string Text,
    string Muted,
    string Grid,
    string Axis,
    string Accent,
    string AccentText,
    string Bar,
    string[] Heatmap);

// Cards ship as a single file that adapts to the reader's color scheme. Light is the default so that a
// viewer which ignores the media query still renders the card the way it always looked.
internal static class Theme
{
    private const string FontStack = "-apple-system,BlinkMacSystemFont,'Segoe UI',Ubuntu,'Helvetica Neue',Arial,sans-serif";

    public static readonly Palette Light = new(
        CardFill: "#ffffff",
        CardStroke: "#d0d7de",
        Title: "#0969da",
        Text: "#1f2328",
        Muted: "#57606a",
        Grid: "#eaeef2",
        Axis: "#d0d7de",
        Accent: "#2da44e",
        AccentText: "#1a7f37",
        Bar: "#0969da",
        Heatmap: ["#ebedf0", "#9be9a8", "#40c463", "#30a14e", "#216e39"]);

    public static readonly Palette Dark = new(
        CardFill: "#0d1117",
        CardStroke: "#30363d",
        Title: "#58a6ff",
        Text: "#c9d1d9",
        Muted: "#8b949e",
        Grid: "#21262d",
        Axis: "#30363d",
        Accent: "#39d353",
        AccentText: "#39d353",
        Bar: "#1f6feb",
        Heatmap: ["#161b22", "#0e4429", "#006d32", "#26a641", "#39d353"]);

    public static string Css() =>
        $"text{{font-family:{FontStack}}}" +
        ".n{font-variant-numeric:tabular-nums}" +
        Rules(Light) +
        $"@media(prefers-color-scheme:dark){{{Rules(Dark)}}}";

    private static string Rules(Palette palette) =>
        $".card{{fill:{palette.CardFill};stroke:{palette.CardStroke}}}" +
        $".tt{{fill:{palette.Title}}}" +
        $".tp{{fill:{palette.Text}}}" +
        $".tm{{fill:{palette.Muted}}}" +
        $".gl{{stroke:{palette.Grid}}}" +
        $".ax{{stroke:{palette.Axis}}}" +
        $".ac{{fill:{palette.Accent}}}" +
        $".acs{{stroke:{palette.Accent}}}" +
        $".act{{fill:{palette.AccentText}}}" +
        $".bar{{fill:{palette.Bar}}}" +
        $".dot{{fill:{palette.Accent};stroke:{palette.CardFill}}}" +
        String.Concat(palette.Heatmap.Select(static (color, index) => $".h{index}{{fill:{color}}}"));
}
