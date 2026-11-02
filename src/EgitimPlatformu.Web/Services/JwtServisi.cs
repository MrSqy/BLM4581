using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
namespace EgitimPlatformu.Web.Services;

public sealed record TokenYaniti(string AccessToken, string TokenType, DateTime ExpiresAt);
public sealed class JwtServisi(IConfiguration configuration)
{
    public const string Issuer = "EgitimPlatformu";
    public const string Audience = "EgitimPlatformu.Mobile";
    public SymmetricSecurityKey? Anahtar()
    {
        var deger = configuration["Jwt:Anahtar"];
        if (string.IsNullOrWhiteSpace(deger)) return null;
        try
        {
            var bytes = Convert.FromBase64String(deger);
            return bytes.Length >= 32 ? new SymmetricSecurityKey(bytes) : null;
        }
        catch (FormatException) { return null; }
    }
    public TokenYaniti Uret(IdentityUser user, IEnumerable<string> roller)
    {
        var anahtar = Anahtar() ?? throw new InvalidOperationException("JWT anahtarı yapılandırılmadı.");
        var simdi = DateTime.UtcNow;
        var bitis = simdi.AddMinutes(15);
        var claims = new List<Claim>
        {
            new("sub", user.Id), new("jti", Guid.NewGuid().ToString()),
            new("name", user.UserName ?? ""), new("stamp", user.SecurityStamp ?? "")
        };
        claims.AddRange(roller.Select(r => new Claim("role", r)));
        var token = new JwtSecurityToken(Issuer, Audience, claims, simdi, bitis,
            new SigningCredentials(anahtar, SecurityAlgorithms.HmacSha256));
        return new(new JwtSecurityTokenHandler().WriteToken(token), "Bearer", bitis);
    }
}
