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
            "--", "python", "-m", "pytest", "-q"
        ];
        return islemCalistirici.CalistirAsync("setsid", argumanlar, cancellationToken);
    }
}

public sealed class HariciIslemCalistirici(int ciktiSiniri = 64 * 1024) : IHariciIslemCalistirici
{
    private readonly int ciktiSiniri = ciktiSiniri > 0
        ? ciktiSiniri
        : throw new ArgumentOutOfRangeException(nameof(ciktiSiniri));

    public async Task<KodCalistirmaSonucu> CalistirAsync(string dosyaAdi, IReadOnlyList<string> argumanlar,
        CancellationToken cancellationToken = default)
    {
        var baslatmaBilgisi = new ProcessStartInfo(dosyaAdi)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var arguman in argumanlar) baslatmaBilgisi.ArgumentList.Add(arguman);

        using var islem = new Process { StartInfo = baslatmaBilgisi };
        cancellationToken.ThrowIfCancellationRequested();
        if (!islem.Start()) throw new InvalidOperationException("Sandbox işlemi başlatılamadı.");

        var sinirAsildi = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var standartCikti = SinirliOkuAsync(islem.StandardOutput, sinirAsildi);
        var standartHata = SinirliOkuAsync(islem.StandardError, sinirAsildi);
        var tamamlandi = islem.WaitForExitAsync();
        var iptalEdildi = Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);

        var ilkTamamlanan = await Task.WhenAny(tamamlandi, sinirAsildi.Task, iptalEdildi);
        if (ilkTamamlanan == iptalEdildi)
        {
            await DurdurAsync(islem);
            await Task.WhenAll(standartCikti, standartHata);
            cancellationToken.ThrowIfCancellationRequested();
        }

        if (ilkTamamlanan == sinirAsildi.Task)
        {
            await DurdurAsync(islem);
            var cikti = await standartCikti;
            var hata = await standartHata;
            return CiktiSiniriSonucu(cikti.Metin, hata.Metin);
        }

        await tamamlandi;
        var normalCikti = await standartCikti;
        var normalHata = await standartHata;
        if (normalCikti.SinirAsildi || normalHata.SinirAsildi)
            return CiktiSiniriSonucu(normalCikti.Metin, normalHata.Metin);
        return new KodCalistirmaSonucu(islem.ExitCode, normalCikti.Metin, normalHata.Metin);
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

    private static async Task DurdurAsync(Process islem)
    {
        if (islem.HasExited) return;

        var nazikceDurduruldu = await SurecGrubunaTermSinyaliGonderAsync(islem.Id);
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
                // Süreç grubu TERM sinyaline yanıt vermediyse aşağıda zorla kapatılır.
            }
        }

        if (!islem.HasExited) islem.Kill(entireProcessTree: true);
        await islem.WaitForExitAsync();
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
