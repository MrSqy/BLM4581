using EgitimPlatformu.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Xunit;
namespace EgitimPlatformu.Web.Tests;

public class JwtSureTests
{
    private static TokenYaniti Uret(string? sure)
    {
        var ayarlar = new Dictionary<string, string?> { ["Jwt:Anahtar"] = Convert.ToBase64String(new byte[32]) };
        if (sure is not null) ayarlar["Jwt:SureDakika"] = sure;
        var config = new ConfigurationBuilder().AddInMemoryCollection(ayarlar).Build();
        return new JwtServisi(config).Uret(new IdentityUser { Id = "1", UserName = "u" }, []);
    }

    [Theory]
    [InlineData(null, 15)]
    [InlineData("120", 120)]
    [InlineData("5", 5)]
    [InlineData("1440", 1440)]
    [InlineData("4", 15)]
    [InlineData("1441", 15)]
    [InlineData("abc", 15)]
    [InlineData("", 15)]
    public void SureAyardanOkunurGecersizseOnBesDakikaKalir(string? ayar, int beklenen)
    {
        var bitis = Uret(ayar).ExpiresAt;
        var hedef = DateTime.UtcNow.AddMinutes(beklenen);
        Assert.InRange(bitis, hedef.AddMinutes(-1), hedef.AddMinutes(1));
    }
}
