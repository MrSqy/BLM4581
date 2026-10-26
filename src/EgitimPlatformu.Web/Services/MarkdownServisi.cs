using Ganss.Xss;
using Markdig;

namespace EgitimPlatformu.Web.Services;

public sealed class MarkdownServisi
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UsePipeTables()
        .DisableHtml()
        .Build();
    private readonly HtmlSanitizer sanitizer = new();

    public MarkdownServisi()
    {
        // Dar izin listesi: HTML/CSS, olay işleyicileri, SVG, iframe ve resim izleyicileri yok.
        sanitizer.AllowedTags.Clear();
        sanitizer.AllowedTags.UnionWith(new[]
        {
            "p", "br", "hr", "h1", "h2", "h3", "h4", "h5", "h6", "strong", "em",
            "blockquote", "ul", "ol", "li", "pre", "code", "a", "table", "thead", "tbody", "tr", "th", "td"
        });
        sanitizer.AllowedAttributes.Clear();
        sanitizer.AllowedAttributes.UnionWith(new[] { "href", "title", "start" });
        sanitizer.AllowedSchemes.Clear();
        sanitizer.AllowedSchemes.UnionWith(new[] { "https", "http", "mailto" });
        sanitizer.AllowDataAttributes = false;
    }

    // Html.Raw yalnızca bu metottan dönen temizlenmiş HTML üzerinde kullanılmalı.
    public string Html(string? markdown) => sanitizer.Sanitize(Markdown.ToHtml(markdown ?? "", Pipeline));
}
