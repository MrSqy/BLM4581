using EgitimPlatformu.Web.Data;
using Microsoft.EntityFrameworkCore;
using EgitimPlatformu.Web.Services;
using Microsoft.Extensions.DependencyInjection.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddScoped<MarkdownServisi>();
// 2.1 tamamlandığında Identity adaptörü bu kaydın yerine geçer.
builder.Services.TryAddScoped<IHesapServisi, BekleyenHesapServisi>();

// Bağlantı dizesi depoda değil, user-secrets'ta tutulur (README > Kurulum).
var baglantiDizesi = builder.Configuration.GetConnectionString("Varsayilan")
    ?? throw new InvalidOperationException(
        "ConnectionStrings:Varsayilan bulunamadı. README'deki user-secrets adımını uygulayın.");
builder.Services.AddDbContext<UygulamaDbContext>(options =>
    options.UseNpgsql(baglantiDizesi));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

app.Run();

// WebApplicationFactory ile gerçek PostgreSQL'e dokunmadan entegrasyon testi.
public partial class Program { }
