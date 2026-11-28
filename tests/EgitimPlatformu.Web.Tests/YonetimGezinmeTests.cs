using System.Net;
using System.Text.RegularExpressions;
using EgitimPlatformu.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EgitimPlatformu.Web.Tests;

public class YonetimGezinmeTests
{
    [Theory]
    [InlineData("/Yonetim", "/Yonetim")]
    [InlineData("/Yonetim/Kategoriler", "/Yonetim/Kategoriler")]
    [InlineData("/Yonetim/Egitimler", "/Yonetim/Egitimler")]
    [InlineData("/Yonetim/Gorevler", "/Yonetim/Gorevler")]
    [InlineData("/Yonetim/Notebooklar", "/Yonetim/Notebooklar")]
    [InlineData("/Yonetim/Kullanicilar", "/Yonetim/Kullanicilar")]
    [InlineData("/Yonetim/Istatistikler", "/Yonetim/Istatistikler")]
    [InlineData("/Yonetim/Kategoriler?DuzenleId=1", "/Yonetim/Kategoriler")]
    public async Task OrtakBaslikVeTekMenuDogruAktifBaglantiyiGosterir(string path, string aktifUrl)
    {
        using var app = new IdentityApplication(); using var c = await app.IstemciAsync();
        await IdentityApplication.KayitAsync(c);
        using (var scope = app.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            Assert.True((await users.AddToRoleAsync((await users.FindByEmailAsync("ogrenci@example.test"))!, Roller.Yonetici)).Succeeded);
        }
        Assert.Equal(HttpStatusCode.Redirect, (await IdentityApplication.GirisAsync(c)).StatusCode);
        var html = await c.GetStringAsync(path);
        var main = Regex.Match(html, "<main\\b[^>]*>.*?</main>", RegexOptions.Singleline).Value;
        Assert.Single(Regex.Matches(main, "class=\"management-heading\""));
        Assert.Single(Regex.Matches(main, "class=\"management-nav\""));
        var heading = Regex.Match(main, "<header\\b[^>]*class=\"management-heading\"[^>]*>.*?</header>", RegexOptions.Singleline).Value;
        Assert.Contains("href=\"/Yonetim\"", heading);
        var menu = Regex.Match(main, "<nav\\b[^>]*class=\"management-nav\"[^>]*>.*?</nav>", RegexOptions.Singleline).Value;
        var labels = Regex.Matches(menu, "<a[^>]*>([^<]+)</a>").Select(m => WebUtility.HtmlDecode(m.Groups[1].Value)).ToArray();
        Assert.Equal(new[] { "Kategoriler", "Eğitimler", "Görevler", "Notebook’lar", "Kullanıcılar", "İstatistikler" }, labels);
        Assert.DoesNotContain(">Panel</a>", main);
        var active = Assert.Single(Regex.Matches(heading + menu, "<a\\b[^>]*aria-current=\"page\"[^>]*>"));
        Assert.Contains($"href=\"{aktifUrl}\"", active.Value);
        if (path == "/Yonetim")
        {
            Assert.DoesNotContain("content-panel", main);
            Assert.DoesNotContain("class=\"row", main);
            Assert.Single(Regex.Matches(main, "<h1\\b"));
        }
    }
}
