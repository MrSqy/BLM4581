using System.Net;
using System.Text.RegularExpressions;
using EgitimPlatformu.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EgitimPlatformu.Web.Tests;

public class NavbarTests
{
    [Theory]
    [InlineData("misafir")]
    [InlineData("ogrenci")]
    [InlineData("yonetici")]
    [InlineData("ikirol")]
    public async Task NavbarRoluneGoreGorunurVeYoneticiOnceliklidir(string durum)
    {
        using var app = new IdentityApplication(); using var c = await app.IstemciAsync();
        var yonetici = durum is "yonetici" or "ikirol";
        if (durum != "misafir")
        {
            Assert.Equal(HttpStatusCode.Redirect, (await IdentityApplication.KayitAsync(c)).StatusCode);
            if (yonetici)
            {
                using var scope = app.Services.CreateScope();
                var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
                var user = (await users.FindByEmailAsync("ogrenci@example.test"))!;
                Assert.True((await users.AddToRoleAsync(user, Roller.Yonetici)).Succeeded);
                if (durum == "yonetici") Assert.True((await users.RemoveFromRoleAsync(user, Roller.Ogrenci)).Succeeded);
            }
            Assert.Equal(HttpStatusCode.Redirect, (await IdentityApplication.GirisAsync(c)).StatusCode);
        }

        var html = await c.GetStringAsync("/");
        var nav = Regex.Match(html, "<nav\\b[^>]*>.*?</nav>", RegexOptions.Singleline).Value;
        var menu = Regex.Match(nav, "<ul\\b[^>]*class=\"navbar-nav\"[^>]*>(.*?)</ul>", RegexOptions.Singleline).Groups[1].Value;
        Assert.NotEmpty(menu);
        var metinler = Regex.Matches(menu, "<a[^>]*>([^<]+)</a>").Select(m => WebUtility.HtmlDecode(m.Groups[1].Value)).ToArray();
        Assert.Equal(yonetici ? new[] { "Eğitimler", "Kategoriler", "Yönetim" }
            : durum == "misafir" ? new[] { "Eğitimler", "Kategoriler", "Nasıl çalışır?" }
            : new[] { "Eğitimler", "Kategoriler", "Nasıl çalışır?", "Panom", "Gönderimlerim" }, metinler);
        Assert.Contains("class=\"site-search\"", nav);
        Assert.Contains("name=\"q\"", nav);
        Assert.Contains("data-bs-target=\"#anaMenu\"", nav);
        Assert.Contains("class=\"collapse navbar-collapse\" id=\"anaMenu\"", nav);
        if (durum == "misafir")
        {
            Assert.Contains("href=\"/Hesap/Giris\"", nav); Assert.Contains("href=\"/Hesap/Kaydol\"", nav);
            Assert.DoesNotContain("action=\"/Hesap/Cikis\"", nav);
        }
        else
        {
            Assert.Contains("ogrenci@example.test", nav); Assert.Contains("action=\"/Hesap/Cikis\"", nav);
            Assert.Contains("__RequestVerificationToken", nav);
            // Navbar gizlemesi sunucu yetkilerini değiştirmez.
            Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/Pano")).StatusCode);
            Assert.Equal(yonetici ? HttpStatusCode.OK : HttpStatusCode.Redirect, (await c.GetAsync("/Yonetim")).StatusCode);
        }
    }
}
