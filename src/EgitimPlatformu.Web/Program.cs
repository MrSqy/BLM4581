using EgitimPlatformu.Web.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();

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
