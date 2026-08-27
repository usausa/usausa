using System.Globalization;
using System.Text;

namespace StatsGenerator.Cards;

internal sealed class SvgBuilder
{
    public const string AnchorStart = "start";
    public const string AnchorMiddle = "middle";
    public const string AnchorEnd = "end";

    private readonly StringBuilder builder = new();

    public int Width { get; }

    public int Height { get; }

    public SvgBuilder(int width, int height, string label)
    {
        Width = width;
        Height = height;

        builder.Append(CultureInfo.InvariantCulture, $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{width}\" height=\"{height}\" viewBox=\"0 0 {width} {height}\" role=\"img\" aria-label=\"{Escape(label)}\">");
        builder.Append(CultureInfo.InvariantCulture, $"<style>{Theme.Css()}</style>");
        builder.Append(CultureInfo.InvariantCulture, $"<rect class=\"card\" x=\"0.5\" y=\"0.5\" width=\"{width - 1}\" height=\"{height - 1}\" rx=\"6\"/>");
    }

    public SvgBuilder Text(double x, double y, double size, string style, string value, string anchor = AnchorStart, int weight = 400)
    {
        builder.Append(CultureInfo.InvariantCulture, $"<text class=\"{style}\" x=\"{N(x)}\" y=\"{N(y)}\" font-size=\"{N(size)}\"");
        if (anchor != AnchorStart)
        {
            builder.Append(CultureInfo.InvariantCulture, $" text-anchor=\"{anchor}\"");
        }

        if (weight != 400)
        {
            builder.Append(CultureInfo.InvariantCulture, $" font-weight=\"{weight}\"");
        }

        builder.Append(CultureInfo.InvariantCulture, $">{Escape(value)}</text>");
        return this;
    }

    public SvgBuilder Rect(double x, double y, double width, double height, string? style, double radius = 0, string? fill = null)
    {
        builder.Append(CultureInfo.InvariantCulture, $"<rect{Class(style)} x=\"{N(x)}\" y=\"{N(y)}\" width=\"{N(width)}\" height=\"{N(height)}\"");
        if (radius > 0)
        {
            builder.Append(CultureInfo.InvariantCulture, $" rx=\"{N(radius)}\"");
        }

        if (fill is not null)
        {
            builder.Append(CultureInfo.InvariantCulture, $" fill=\"{fill}\"");
        }

        builder.Append("/>");
        return this;
    }

    public SvgBuilder Line(double x1, double y1, double x2, double y2, string style)
    {
        builder.Append(CultureInfo.InvariantCulture, $"<line class=\"{style}\" x1=\"{N(x1)}\" y1=\"{N(y1)}\" x2=\"{N(x2)}\" y2=\"{N(y2)}\"/>");
        return this;
    }

    public SvgBuilder Circle(double cx, double cy, double r, string? style, string? fill = null, double strokeWidth = 0)
    {
        builder.Append(CultureInfo.InvariantCulture, $"<circle{Class(style)} cx=\"{N(cx)}\" cy=\"{N(cy)}\" r=\"{N(r)}\"");
        if (fill is not null)
        {
            builder.Append(CultureInfo.InvariantCulture, $" fill=\"{fill}\"");
        }

        if (strokeWidth > 0)
        {
            builder.Append(CultureInfo.InvariantCulture, $" stroke-width=\"{N(strokeWidth)}\"");
        }

        builder.Append("/>");
        return this;
    }

    public SvgBuilder Path(string d, string style, string? extra = null)
    {
        builder.Append(CultureInfo.InvariantCulture, $"<path class=\"{style}\" d=\"{d}\"{(extra is null ? String.Empty : " " + extra)}/>");
        return this;
    }

    public SvgBuilder Icon(string path, double x, double y, string style = "tm", double scale = 1)
    {
        builder.Append(CultureInfo.InvariantCulture, $"<g transform=\"translate({N(x)},{N(y)})");
        if (scale != 1)
        {
            builder.Append(CultureInfo.InvariantCulture, $" scale({N(scale)})");
        }

        builder.Append(CultureInfo.InvariantCulture, $"\"><path class=\"{style}\" d=\"{path}\"/></g>");
        return this;
    }

    public string Build() => builder.Append("</svg>").ToString();

    private static string Class(string? style) => String.IsNullOrEmpty(style) ? String.Empty : $" class=\"{style}\"";

    public static string N(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    public static string Number(long value) => value.ToString("N0", CultureInfo.InvariantCulture);

    public static string Escape(string value) => value
        .Replace("&", "&amp;", StringComparison.Ordinal)
        .Replace("<", "&lt;", StringComparison.Ordinal)
        .Replace(">", "&gt;", StringComparison.Ordinal)
        .Replace("\"", "&quot;", StringComparison.Ordinal);
}
