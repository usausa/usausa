using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace StatsGenerator;

// Category titles carry GitHub markdown shortcodes such as :four_leaf_clover:, which only the markdown
// renderer expands. The emoji endpoint maps every shortcode to an asset URL whose path carries the
// codepoints (.../unicode/1f340.png -> U+1F340), so the generated pages can expand them without a
// hand-maintained table.
internal sealed partial class EmojiResolver
{
    private readonly Dictionary<string, string> map;

    private EmojiResolver(Dictionary<string, string> map) => this.map = map;

    public static async Task<EmojiResolver> LoadAsync(string? token)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("usausa-stats-generator", "1.0"));
            if (!String.IsNullOrEmpty(token))
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            using var document = JsonDocument.Parse(await client.GetStringAsync("https://api.github.com/emojis"));
            foreach (var entry in document.RootElement.EnumerateObject())
            {
                var character = ToCharacter(entry.Value.GetString());
                if (character is not null)
                {
                    map[entry.Name] = character;
                }
            }
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException)
        {
            Console.Error.WriteLine($"  warning: emoji table unavailable ({e.Message}), shortcodes left as written");
        }

        return new EmojiResolver(map);
    }

    public string Resolve(string text) => ShortcodePattern().Replace(
        text,
        match => map.TryGetValue(match.Groups[1].Value, out var character) ? character : match.Value);

    private static string? ToCharacter(string? url)
    {
        var match = UnicodePattern().Match(url ?? String.Empty);
        if (!match.Success)
        {
            return null;
        }

        var builder = new StringBuilder();
        foreach (var part in match.Groups[1].Value.Split('-'))
        {
            builder.Append(Char.ConvertFromUtf32(Int32.Parse(part, NumberStyles.HexNumber, CultureInfo.InvariantCulture)));
        }

        return builder.ToString();
    }

    [GeneratedRegex(":([a-z0-9_+-]+):", RegexOptions.IgnoreCase)]
    private static partial Regex ShortcodePattern();

    [GeneratedRegex(@"/unicode/([0-9a-f]+(?:-[0-9a-f]+)*)\.png")]
    private static partial Regex UnicodePattern();
}
