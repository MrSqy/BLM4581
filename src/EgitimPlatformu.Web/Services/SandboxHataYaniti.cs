using Microsoft.AspNetCore.Mvc;

namespace EgitimPlatformu.Web.Services;

// 4.4: Kod çalıştırma uçlarının ortak 503 yanıtı. Durum adları istemcide ayrı mesaj göstermek içindir:
//   kuyruk_dolu  → sandbox yoğun; Retry-After saniye sonra yeniden denenebilir
//   altyapi_yok  → Docker/sandbox şu anda kullanılamıyor
public sealed class SandboxHataYaniti : ObjectResult
{
    public const int YenidenDeneSaniye = 5;
    private readonly bool kuyrukDolu;

    private SandboxHataYaniti(bool kuyrukDolu, string mesaj, string durum)
        : base(new { mesaj, durum })
    {
        this.kuyrukDolu = kuyrukDolu;
        StatusCode = StatusCodes.Status503ServiceUnavailable;
    }

    public static SandboxHataYaniti Olustur(Exception? hata) => hata is SandboxMesgulException
        ? new(true, "Kod çalıştırma şu anda yoğun. Birkaç saniye sonra yeniden deneyin.", "kuyruk_dolu")
        : AltyapiYok();

    public static SandboxHataYaniti AltyapiYok() =>
        new(false, "Kod çalıştırma servisi şu anda kullanılamıyor.", "altyapi_yok");

    public override void OnFormatting(ActionContext context)
    {
        base.OnFormatting(context);
        if (kuyrukDolu) context.HttpContext.Response.Headers.RetryAfter = YenidenDeneSaniye.ToString();
    }
}
