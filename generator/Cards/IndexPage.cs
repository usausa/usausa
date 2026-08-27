using System.Text;

namespace StatsGenerator.Cards;

// A browsable catalogue of everything the run produced, served from the same Pages site.
internal static class IndexPage
{
    public static string Render(Settings settings, EmojiResolver emoji, string[] summaryCards, DateTimeOffset generated)
    {
        var html = new StringBuilder();
        html.Append("<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\">");
        html.Append("<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">");
        html.Append($"<title>{SvgBuilder.Escape(settings.User)} · profile cards</title><style>");
        html.Append("body{margin:0;padding:32px;background:#f6f8fa;color:#1f2328;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Ubuntu,'Helvetica Neue',Arial,sans-serif}");
        html.Append("h1{font-size:20px;margin:0 0 4px}h2{font-size:15px;margin:32px 0 12px;font-weight:600}");
        html.Append("p{font-size:13px;color:#57606a;margin:0}");
        html.Append(".grid{display:flex;flex-wrap:wrap;gap:8px;align-items:flex-start}");
        html.Append("img{display:block;border-radius:6px}");
        html.Append("@media(prefers-color-scheme:dark){body{background:#010409;color:#c9d1d9}p{color:#8b949e}}");
        html.Append("</style></head><body>");
        html.Append($"<h1>{SvgBuilder.Escape(settings.User)} · profile cards</h1>");
        html.Append($"<p>Generated {generated:yyyy-MM-dd HH:mm} UTC · cards follow your system color scheme</p>");

        html.Append("<h2>Summary</h2><div class=\"grid\">");
        foreach (var card in summaryCards)
        {
            html.Append($"<a href=\"{card}\"><img src=\"{card}\" alt=\"{card}\" loading=\"lazy\"></a>");
        }

        html.Append("</div>");

        foreach (var category in settings.Categories)
        {
            html.Append($"<h2>{SvgBuilder.Escape(emoji.Resolve(category.Title))}</h2><div class=\"grid\">");
            foreach (var repository in category.Repositories)
            {
                var path = $"repo/{Uri.EscapeDataString(repository.Name)}.svg";
                html.Append($"<a href=\"https://github.com/{SvgBuilder.Escape(settings.User)}/{SvgBuilder.Escape(repository.Name)}\">");
                html.Append($"<img src=\"{path}\" alt=\"{SvgBuilder.Escape(repository.Label)}\" loading=\"lazy\"></a>");
            }

            html.Append("</div>");
        }

        html.Append("</body></html>");
        return html.ToString();
    }
}
