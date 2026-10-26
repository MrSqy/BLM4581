using EgitimPlatformu.Web.Services;
using Xunit;

namespace EgitimPlatformu.Web.Tests;

public sealed class MarkdownTests
{
    [Theory]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("<img src=x onerror=alert(1)>")]
    [InlineData("[tıkla](javascript:alert%281%29)")]
    [InlineData("[tıkla](data:text/html;base64,PHNjcmlwdD4=)")]
    [InlineData("[tıkla](vbscript:msgbox%281%29)")]
    [InlineData("<svg onload=alert(1)><a href='javascript:alert(1)'>x</a></svg>")]
    [InlineData("<iframe srcdoc='<script>alert(1)</script>'></iframe>")]
    [InlineData("![izleyici](https://example.test/tracker.png)")]
    public void TehlikeliHtmlVeLinklerCalistirilabilirOlmaz(string markdown)
    {
        var html = new MarkdownServisi().Html(markdown);
        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<img", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<svg", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<iframe", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("href=\"javascript:", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("href=\"data:", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("href=\"vbscript:", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NormalMarkdownVeKodKorunur()
    {
        var html = new MarkdownServisi().Html("# Ders\n\n**Kalın** ve `kod`\n\n```python\nprint('<script>')\n```\n\n[Kaynak](https://example.test)");
        Assert.Contains("<h1>Ders</h1>", html);
        Assert.Contains("<strong>Kalın</strong>", html);
        Assert.Contains("<pre><code>", html);
        Assert.Contains("&lt;script&gt;", html);
        Assert.Contains("href=\"https://example.test\"", html);
    }
}
