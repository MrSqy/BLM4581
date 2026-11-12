using EgitimPlatformu.Web.Data;
using Microsoft.EntityFrameworkCore;
using EgitimPlatformu.Web.Services;
using Microsoft.AspNetCore.Identity;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddControllers();
builder.Services.AddAntiforgery(options => options.HeaderName = "X-CSRF-TOKEN");
builder.Services.AddScoped<IcerikAktarimServisi>();
builder.Services.AddScoped<MarkdownServisi>();
builder.Services.AddScoped<GonderimServisi>();
builder.Services.AddScoped<IGonderimGecmisi>(services => services.GetRequiredService<GonderimServisi>());
builder.Services.AddScoped<IHesapServisi, IdentityHesapServisi>();
builder.Services.AddSingleton(SandboxKuyruguAyarlari.Oku(builder.Configuration));
builder.Services.AddSingleton<IHariciIslemCalistirici>(services =>
    new KuyrukluIslemCalistirici(new HariciIslemCalistirici(),
        services.GetRequiredService<SandboxKuyruguAyarlari>()));
builder.Services.AddSingleton<ISandboxIslemCalistirici>(services =>
{
    return new BashSandboxIslemCalistirici(AppContext.BaseDirectory,
        services.GetRequiredService<IHariciIslemCalistirici>());
});
builder.Services.AddSingleton<IKodCalistirmaServisi, PythonKodCalistirmaServisi>();
builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
{
    options.User.RequireUniqueEmail = true;
    options.Password.RequiredLength = 8;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
}).AddEntityFrameworkStores<UygulamaDbContext>().AddDefaultTokenProviders();
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Hesap/Giris";
    options.AccessDeniedPath = "/Hesap/Giris";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.Events.OnRedirectToLogin = context =>
    {
        if (context.Request.Path.StartsWithSegments("/api")) context.Response.StatusCode = 401;
        else context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = context =>
    {
        if (context.Request.Path.StartsWithSegments("/api")) context.Response.StatusCode = 403;
        else context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };
});

builder.Services.MobilApiEkle();
builder.Services.AddSingleton<IGorevKodServisi, GorevKodServisi>();

// Bağlantı dizesi depoda değil, user-secrets'ta tutulur (README > Kurulum).
var baglantiDizesi = builder.Configuration.GetConnectionString("Varsayilan")
    ?? throw new InvalidOperationException(
        "ConnectionStrings:Varsayilan bulunamadı. README'deki user-secrets adımını uygulayın.");
builder.Services.AddDbContext<UygulamaDbContext>(options =>
    options.UseNpgsql(baglantiDizesi));

ApiKurulumu.Ekle(builder.Services, builder.Configuration);

var app = builder.Build();

if (args.Contains("--identity-hazirla"))
{
    await IdentityBaslangic.HazirlaAsync(app.Services, app.Configuration);
    return;
}

var icerikKomutu = Array.FindIndex(args, a => a is "--icerik-planla" or "--icerik-aktar");
if (icerikKomutu >= 0)
{
    if (icerikKomutu + 1 >= args.Length) throw new InvalidOperationException("Paket dosya yolunu belirtin.");
    await IcerikYerelKomutu.Calistir(app.Services, args[icerikKomutu + 1], args[icerikKomutu] == "--icerik-planla");
    return;
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllers();
app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

app.Run();

// WebApplicationFactory ile gerçek PostgreSQL'e dokunmadan entegrasyon testi.
public partial class Program { }
