using System.Text.RegularExpressions;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace ltwnc.Services.Lessons;

/// <summary>
/// Renders Markdown to safe HTML: raw HTML disabled; only http(s)/mailto links kept.
/// </summary>
public static class LessonMarkdownRenderer
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseEmphasisExtras()
        .UsePipeTables()
        .UseAutoLinks()
        .DisableHtml()
        .Build();

    public static string ToHtml(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return string.Empty;
        }

        MarkdownDocument document = Markdown.Parse(markdown, Pipeline);
        SanitizeLinks(document);
        return Markdown.ToHtml(document, Pipeline);
    }

    private static void SanitizeLinks(MarkdownObject node)
    {
        if (node is LinkInline link)
        {
            if (!IsSafeUrl(link.Url))
            {
                link.Url = "#";
            }
        }

        if (node is ContainerBlock containerBlock)
        {
            foreach (Block child in containerBlock)
            {
                SanitizeLinks(child);
            }
        }
        else if (node is LeafBlock { Inline: not null } leaf)
        {
            for (Inline? inline = leaf.Inline; inline != null; inline = inline.NextSibling)
            {
                SanitizeInlines(inline);
            }
        }
        else if (node is ContainerInline containerInline)
        {
            for (Inline? inline = containerInline.FirstChild; inline != null; inline = inline.NextSibling)
            {
                SanitizeInlines(inline);
            }
        }
    }

    private static void SanitizeInlines(Inline inline)
    {
        if (inline is LinkInline link && !IsSafeUrl(link.Url))
        {
            link.Url = "#";
        }

        if (inline is ContainerInline container)
        {
            for (Inline? child = container.FirstChild; child != null; child = child.NextSibling)
            {
                SanitizeInlines(child);
            }
        }
    }

    private static bool IsSafeUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        string value = url.Trim();
        if (value.StartsWith('#') || value.StartsWith('/'))
        {
            return true;
        }

        return Regex.IsMatch(value, "^(https?:|mailto:)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }
}
