using System.Globalization;

namespace StatsGenerator.Cards;

// SVG has no text layout, so descriptions are wrapped against an estimated advance width.
// Values are fractions of the em box, approximating the card font stack.
internal static class TextMeasure
{
    private const double Narrow = 0.28;
    private const double Thin = 0.34;
    private const double Digit = 0.55;
    private const double Lower = 0.53;
    private const double Upper = 0.66;
    private const double Wide = 0.85;
    private const double FullWidth = 1.0;
    private const double Pictograph = 1.2;

    // Hangul Jamo and above covers the CJK and fullwidth blocks that occupy a whole em.
    private const char FullWidthStart = 'ᄀ';

    public static double Width(string text, double fontSize)
    {
        var total = 0.0d;
        var enumerator = StringInfo.GetTextElementEnumerator(text);
        while (enumerator.MoveNext())
        {
            total += Advance((string)enumerator.Current);
        }

        return total * fontSize;
    }

    public static string[] Wrap(string text, double fontSize, double maxWidth, int maxLines)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var lines = new List<string>();
        var current = String.Empty;

        foreach (var word in words)
        {
            var candidate = current.Length == 0 ? word : $"{current} {word}";
            if ((current.Length > 0) && (Width(candidate, fontSize) > maxWidth))
            {
                lines.Add(current);
                if (lines.Count == maxLines)
                {
                    return Finish(lines, text, fontSize, maxWidth);
                }

                current = word;
            }
            else
            {
                current = candidate;
            }
        }

        if (current.Length > 0)
        {
            lines.Add(current);
        }

        return Finish(lines, text, fontSize, maxWidth);
    }

    private static string[] Finish(List<string> lines, string text, double fontSize, double maxWidth)
    {
        // A single word longer than the card still has to fit, and the last line absorbs whatever was dropped.
        for (var i = 0; i < lines.Count; i++)
        {
            if (Width(lines[i], fontSize) > maxWidth)
            {
                lines[i] = Truncate(lines[i], fontSize, maxWidth);
            }
        }

        if ((lines.Count > 0) && (String.Join(' ', lines).Length < text.Length) && !lines[^1].EndsWith('…'))
        {
            lines[^1] = Truncate($"{lines[^1]}…", fontSize, maxWidth);
        }

        return [.. lines];
    }

    private static string Truncate(string text, double fontSize, double maxWidth)
    {
        var value = text;
        while ((value.Length > 1) && (Width(value, fontSize) > maxWidth))
        {
            value = value[..^1].TrimEnd('…').TrimEnd();
        }

        return value.EndsWith('…') ? value : $"{value}…";
    }

    private static double Advance(string element)
    {
        if (element.Length > 1)
        {
            return Pictograph;
        }

        return element[0] switch
        {
            ' ' => Narrow,
            >= '0' and <= '9' => Digit,
            'i' or 'j' or 'l' or 'I' or '.' or ',' or ':' or ';' or '\'' or '`' or '|' or '!' => Narrow,
            'f' or 't' or 'r' or '(' or ')' or '[' or ']' or '{' or '}' or '/' or '\\' or '-' => Thin,
            'm' or 'w' or 'M' or 'W' => Wide,
            >= 'a' and <= 'z' => Lower,
            >= 'A' and <= 'Z' => Upper,
            >= FullWidthStart => FullWidth,
            _ => Lower
        };
    }
}
