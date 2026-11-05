using System.Collections;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using EgitimPlatformu.Web.Contracts;
using EgitimPlatformu.Web.Data;
using Microsoft.EntityFrameworkCore;
namespace EgitimPlatformu.Web.Services;

// Açık yerel yönetim komutu; normal uygulama açılışında çalışmaz.
public static class IcerikYerelKomutu
{
    public static async Task Calistir(IServiceProvider services, string dosya, bool plan)
    {
        var info = new FileInfo(dosya);
        if (info.Length > 5 * 1024 * 1024) throw new InvalidOperationException("Paket en fazla 5 MiB olabilir.");
        var paket = JsonSerializer.Deserialize<IcerikPaketi>(await File.ReadAllTextAsync(dosya), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Dogrula(paket);
        var hata = IcerikAktarimServisi.TutarlilikHatasi(paket);
        if (hata is not null) throw new InvalidOperationException(hata);
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<UygulamaDbContext>();
        if (plan)
        {
            foreach (var ders in paket.Kategoriler.SelectMany(k => k.Egitimler))
            {
                var eski = await db.Gorevler.AsNoTracking().Where(g => g.Egitim!.Kod == ders.Kod).Select(g => new { g.No, g.Aktif }).ToListAsync();
                var pasif = paket.Surum == 2 ? eski.Where(g => g.Aktif && !ders.Gorevler.Any(k => k.No == g.No)).Select(g => g.No).ToArray() : [];
                Console.WriteLine($"{ders.Kod}: {ders.Gorevler.Count} görev yazılacak; pasifleşecek numaralar: {string.Join(",", pasif)}. Silme yok.");
            }
            return;
        }
        var result = await scope.ServiceProvider.GetRequiredService<IcerikAktarimServisi>().AktarAsync(paket, default);
        Console.WriteLine($"Aktarım tamamlandı: {result.EgitimSayisi} ders, {result.GorevSayisi} görev. Kullanıcılar ve pasif görev kayıtları korundu.");
    }
    private static void Dogrula(object value)
    {
        Validator.ValidateObject(value, new ValidationContext(value), true);
        foreach (var property in value.GetType().GetProperties())
        {
            var child = property.GetValue(value);
            if (child is null || child is string || child.GetType().IsValueType) continue;
            if (child is IEnumerable items) { foreach (var item in items) if (item is not null && item is not string) Dogrula(item); }
            else Dogrula(child);
        }
    }
}
