using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;
namespace EgitimPlatformu.Web.Services;

public static class ApiKurulumu
{
    public static void Ekle(IServiceCollection services, IConfiguration configuration)
    {
        var jwt = new JwtServisi(configuration);
        services.AddSingleton(jwt);
        services.AddScoped<DersErisimi>();
        services.AddAuthentication().AddJwtBearer(options =>
        {
            options.MapInboundClaims = false;
            options.IncludeErrorDetails = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true, ValidIssuer = JwtServisi.Issuer,
                ValidateAudience = true, ValidAudience = JwtServisi.Audience,
                ValidateLifetime = true, RequireExpirationTime = true,
                ValidateIssuerSigningKey = true, RequireSignedTokens = true,
                ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                IssuerSigningKeyResolver = (_, _, _, _) => jwt.Anahtar() is { } key ? [key] : [],
                ClockSkew = TimeSpan.FromSeconds(30), NameClaimType = "name", RoleClaimType = "role"
            };
            options.Events = new JwtBearerEvents
            {
                OnTokenValidated = async context =>
                {
                    var users = context.HttpContext.RequestServices.GetRequiredService<UserManager<IdentityUser>>();
                    var principal = context.Principal!;
                    var user = await users.FindByIdAsync(principal.FindFirst("sub")?.Value ?? "");
                    if (user is null || user.SecurityStamp != principal.FindFirst("stamp")?.Value || await users.IsLockedOutAsync(user))
                    {
                        context.Fail("Hesap veya oturum geçersiz."); return;
                    }
                    var roller = await users.GetRolesAsync(user);
                    if (!roller.Order().SequenceEqual(principal.FindAll("role").Select(c => c.Value).Order()))
                        context.Fail("Hesap rolleri değişti.");
                }
            };
        });
        services.AddAuthorization(options => options.AddPolicy("ApiYonetici", policy =>
            policy.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser().RequireRole(Roller.Yonetici)));
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo { Title = "Eğitim Platformu API", Version = "v1" });
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT",
                Description = "/api/hesap/giris yanıtındaki accessToken değerini girin."
            });
            options.OperationFilter<JwtSwaggerFiltresi>();
        });
    }
}

// Anonim erişim de sunan, ancak token geldiğinde onu kullanan uçların API belgesi.
[AttributeUsage(AttributeTargets.Method)]
public sealed class IstegeBagliBearerAttribute : Attribute { }

public sealed class JwtSwaggerFiltresi : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var method = context.MethodInfo;
        if (method.GetCustomAttributes(true).OfType<AllowAnonymousAttribute>().Any()) return;
        var auth = method.GetCustomAttributes(true).OfType<AuthorizeAttribute>()
            .Concat(method.DeclaringType!.GetCustomAttributes(true).OfType<AuthorizeAttribute>());
        var optional = method.IsDefined(typeof(IstegeBagliBearerAttribute), true);
        if (!auth.Any() && !optional) return;
        operation.Security = [new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference("Bearer", context.Document)] = [] }];
        if (optional) operation.Security.Add(new OpenApiSecurityRequirement());
        operation.Responses ??= [];
        operation.Responses.TryAdd("401", new OpenApiResponse { Description = "Geçerli JWT gerekli." });
        if (auth.Any(a => a.Policy == "ApiYonetici" || a.Roles == Roller.Yonetici))
            operation.Responses.TryAdd("403", new OpenApiResponse { Description = "Yönetici rolü gerekli." });
    }
}
