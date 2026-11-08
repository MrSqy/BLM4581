using System.Text.Json;
using EgitimPlatformu.Web.Contracts;
using EgitimPlatformu.Web.Models;
namespace EgitimPlatformu.Web.Services;

public sealed record GorevKodSonucu(int? No, bool Basarili, string Durum, string Cikti, string Hata,
    int? CikisKodu, bool? CiktiEslesiyor, bool? CikisKoduEslesiyor, IReadOnlyList<string> KuralIhlalleri);
public interface IGorevKodServisi
{
    Task<GorevKodSonucu> CalistirAsync(Gorev gorev, string kod, bool test, CancellationToken ct);
}
public sealed class GorevKodServisi(IHariciIslemCalistirici process) : IGorevKodServisi
{
    public async Task<GorevKodSonucu> CalistirAsync(Gorev gorev, string kod, bool test, CancellationToken ct)
    {
        var kontrol = JsonSerializer.Deserialize<GorevKontrolu>(gorev.KontrolJson!)!;
        // Eşzamanlılık ve sıra ortak sandbox kuyruğunda (KuyrukluIslemCalistirici) tutulur.
        var dir = Path.Combine(Path.GetTempPath(), "egitim-gorev-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(dir, "gorevler"));
            foreach (var d in new[] { dir, Path.Combine(dir, "gorevler") })
                if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(d, (UnixFileMode)493);
            // Başlangıç adı yalnız güvenli bir dosya adı olarak kullanılır; istemci yol gönderemez.
            var dosya = gorev.BaslangicDosyasi!;
            if (!System.Text.RegularExpressions.Regex.IsMatch(dosya, @"\Agorevler/[a-zA-Z0-9_-]+\.py\z")) throw new InvalidOperationException("Geçersiz görev dosyası.");
            await File.WriteAllTextAsync(Path.Combine(dir, dosya), kod, ct);
            // Beklenen çıktı ve çıkış kodu sandbox'a verilmez; karşılaştırma sunucuda yapılır.
            await File.WriteAllTextAsync(Path.Combine(dir, "istek.json"), JsonSerializer.Serialize(new { dosya, girdi = kontrol.Girdi, kullanilmamali = kontrol.Kullanilmamali }), ct);
            File.Copy(Path.Combine(AppContext.BaseDirectory, "scripts", "task-runner.py"), Path.Combine(dir, "runner.py"));
            foreach (var file in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
                if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(file, (UnixFileMode)420);
            var result = await process.CalistirAsync("setsid", ["bash", Path.Combine(AppContext.BaseDirectory, "scripts", "run-python-sandbox.sh"),
                "--timeout", "8", "--volume", $"{dir}:/calisma:ro", "--workdir", "/calisma",
                "--env", "PYTHONDONTWRITEBYTECODE=1", "--", "python", "-B", "runner.py"], ct);
            if (result.AltyapiHatasi) throw new InvalidOperationException("Sandbox altyapısı yok.");
            if (result.ZamanAsimi) return new(gorev.No, false, "zaman_asimi", "", "Süre sınırı aşıldı.", null, null, null, []);
            if (!result.Basarili) throw new InvalidOperationException("Sandbox yanıtı tamamlanamadı.");
            using var doc = JsonDocument.Parse(result.StandartCikti);
            var data = doc.RootElement;
            var cikti = data.GetProperty("cikti").GetString()!;
            var hata = data.GetProperty("hata").GetString()!;
            var exit = data.GetProperty("cikisKodu").GetInt32();
            var ihlaller = data.GetProperty("kuralIhlalleri").EnumerateArray().Select(x => x.GetString()!).ToArray();
            if (data.GetProperty("sinirAsildi").GetBoolean())
                return new(gorev.No, false, "cikti_siniri", cikti, hata, exit, null, null, ihlaller);
            var outputOk = cikti == kontrol.BeklenenCikti;
            var exitOk = exit == kontrol.BeklenenCikisKodu;
            var ok = test ? outputOk && exitOk && ihlaller.Length == 0 : exit == 0;
            return new(gorev.No, ok, test ? (ok ? "basarili" : "basarisiz") : (ok ? "tamamlandi" : "hata"),
                cikti, hata, exit, test ? outputOk : null, test ? exitOk : null, test ? ihlaller : []);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or IOException)
        { throw new InvalidOperationException("Sandbox yanıtı okunamadı.", ex); }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }
}
