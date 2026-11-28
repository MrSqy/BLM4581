using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace EgitimPlatformu.Web.Services;

public interface IHariciIslemCalistirici
{
    Task<KodCalistirmaSonucu> CalistirAsync(string dosyaAdi, IReadOnlyList<string> argumanlar,
        CancellationToken cancellationToken = default);
}

public sealed class BashSandboxIslemCalistirici(string uygulamaDizini, IHariciIslemCalistirici islemCalistirici)
    : ISandboxIslemCalistirici
{
    public Task<KodCalistirmaSonucu> CalistirAsync(SandboxCalistirmaIstegi istek,
        CancellationToken cancellationToken = default)
    {
        var betikYolu = Path.Combine(uygulamaDizini, "scripts", "run-python-sandbox.sh");
        IReadOnlyList<string> argumanlar =
        [
            "bash",
            betikYolu,
            "--timeout", istek.SureSiniriSaniye.ToString(),
            "--volume", $"{istek.CalismaDizini}:/calisma:ro",
            "--workdir", "/calisma",
            "--", "python", "-m", "pytest", "-v"
        ];
        return islemCalistirici.CalistirAsync("setsid", argumanlar, cancellationToken);
    }
}

public sealed class HariciIslemCalistirici(int ciktiSiniri = 64 * 1024) : IHariciIslemCalistirici
{
    private const string WslBaslamaSatiri = "EGITIM_SANDBOX_WSL_BASLADI\n";

    private readonly int ciktiSiniri = ciktiSiniri > 0
        ? ciktiSiniri
        : throw new ArgumentOutOfRangeException(nameof(ciktiSiniri));

    public async Task<KodCalistirmaSonucu> CalistirAsync(string dosyaAdi, IReadOnlyList<string> argumanlar,
        CancellationToken cancellationToken = default)
    {
        var wsl = OperatingSystem.IsWindows() && dosyaAdi == "setsid" && argumanlar.Count >= 2 &&
            argumanlar[0] == "bash" && Path.GetFileName(argumanlar[1]) == "run-python-sandbox.sh";
        var baslatmaBilgisi = new ProcessStartInfo(wsl
            ? Path.Combine(Environment.SystemDirectory, "wsl.exe")
            : dosyaAdi)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = wsl,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        if (wsl)
        {
            var betik = Path.GetFullPath(argumanlar[1]);
            foreach (var arguman in new[] { "--cd", Path.GetDirectoryName(betik)!, "--exec",
                "setsid", "--wait", "bash", "./run-python-sandbox.sh", "--wsl" })
                baslatmaBilgisi.ArgumentList.Add(arguman);
            baslatmaBilgisi.StandardInputEncoding = new UTF8Encoding(false);
            baslatmaBilgisi.StandardOutputEncoding = Encoding.UTF8;
            baslatmaBilgisi.StandardErrorEncoding = Encoding.UTF8;
            baslatmaBilgisi.Environment["WSL_UTF8"] = "1";
            argumanlar = argumanlar.Skip(2).ToArray();
        }
        foreach (var arguman in argumanlar) baslatmaBilgisi.ArgumentList.Add(arguman);

        using var islem = new Process { StartInfo = baslatmaBilgisi };
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            if (!islem.Start()) throw new InvalidOperationException("Sandbox işlemi başlatılamadı.");
        }
        catch (Win32Exception ex) when (wsl)
        {
            return new KodCalistirmaSonucu(125, "", $"WSL 2 başlatılamadı: {ex.Message}");
        }

        var sinirAsildi = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var standartCikti = SinirliOkuAsync(islem.StandardOutput, sinirAsildi);
        var standartHata = SinirliOkuAsync(islem.StandardError, sinirAsildi);
        var tamamlandi = islem.WaitForExitAsync();
        var iptalEdildi = Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);

        var ilkTamamlanan = await Task.WhenAny(tamamlandi, sinirAsildi.Task, iptalEdildi);
        if (ilkTamamlanan == iptalEdildi)
        {
            await DurdurAsync(islem, wsl);
            await Task.WhenAll(standartCikti, standartHata);
            cancellationToken.ThrowIfCancellationRequested();
        }

        if (ilkTamamlanan == sinirAsildi.Task)
        {
            await DurdurAsync(islem, wsl);
            var cikti = await standartCikti;
            var hata = await standartHata;
            return CiktiSiniriSonucu(cikti.Metin, hata.Metin);
        }

        await tamamlandi;
        var normalCikti = await standartCikti;
        var normalHata = await standartHata;
        if (normalCikti.SinirAsildi || normalHata.SinirAsildi)
            return CiktiSiniriSonucu(normalCikti.Metin, normalHata.Metin);
        var cikisKodu = islem.ExitCode;
        var hataMetni = normalHata.Metin;
        if (wsl)
        {
            // WSL veya Bash betiği açamadıysa çıkış kodu öğrenci koduna ait değildir.
            var baslamaKonumu = hataMetni.IndexOf(WslBaslamaSatiri, StringComparison.Ordinal);
            if (baslamaKonumu < 0 || cikisKodu is < 0 or > 255) cikisKodu = 125;
            if (baslamaKonumu >= 0) hataMetni = hataMetni.Remove(baslamaKonumu, WslBaslamaSatiri.Length);
        }
        return new KodCalistirmaSonucu(cikisKodu, normalCikti.Metin, hataMetni);
    }

    private static KodCalistirmaSonucu CiktiSiniriSonucu(string cikti, string hata)
    {
        const string aciklama = "Sandbox çıktısı güvenlik çıktı sınırını aştı.";
        return new KodCalistirmaSonucu(125, cikti,
            string.IsNullOrEmpty(hata) ? aciklama : $"{hata}\n{aciklama}");
    }

    private async Task<SinirliOkumaSonucu> SinirliOkuAsync(StreamReader okuyucu,
        TaskCompletionSource sinirAsildi)
    {
        var metin = new StringBuilder(Math.Min(ciktiSiniri, 4096));
        var tampon = new char[4096];
        var asildi = false;
        int okunan;
        while ((okunan = await okuyucu.ReadAsync(tampon)) > 0)
        {
            var kalan = ciktiSiniri - metin.Length;
            if (kalan > 0) metin.Append(tampon, 0, Math.Min(kalan, okunan));
            if (okunan > kalan)
            {
                asildi = true;
                sinirAsildi.TrySetResult();
            }
        }
        return new SinirliOkumaSonucu(metin.ToString(), asildi);
    }

    private static async Task DurdurAsync(Process islem, bool wsl)
    {
        if (islem.HasExited) return;

        var nazikceDurduruldu = wsl
            ? WslGirdisiniKapat(islem)
            : await SurecGrubunaTermSinyaliGonderAsync(islem.Id);
        if (nazikceDurduruldu)
        {
            using var beklemeSiniri = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            try
            {
                await islem.WaitForExitAsync(beklemeSiniri.Token);
                return;
            }
            catch (OperationCanceledException)
            {
                // Betik verilen sürede temizliği bitiremezse başlatıcı zorla kapatılır.
            }
        }

        if (!islem.HasExited) islem.Kill(entireProcessTree: true);
        await islem.WaitForExitAsync();
    }

    private static bool WslGirdisiniKapat(Process islem)
    {
        try
        {
            // EOF, WSL içindeki betiğe çıkmadan önce konteyneri temizlemesini bildirir.
            islem.StandardInput.Close();
            return true;
        }
        catch (IOException)
        {
            return false;
        }
    }

    private static async Task<bool> SurecGrubunaTermSinyaliGonderAsync(int surecKimligi)
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS()) return false;

        using var sinyal = Process.Start(new ProcessStartInfo("kill")
        {
            UseShellExecute = false,
            ArgumentList = { "--", $"-{surecKimligi}" }
        });
        if (sinyal is null) return false;
        await sinyal.WaitForExitAsync();
        return sinyal.ExitCode == 0;
    }

    private sealed record SinirliOkumaSonucu(string Metin, bool SinirAsildi);
}
