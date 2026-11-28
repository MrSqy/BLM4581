using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Net.WebSockets;
using System.Security.Claims;
using System.Text.Json;
using EgitimPlatformu.Web.Data;
using EgitimPlatformu.Web.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace EgitimPlatformu.Web.Controllers;

[ApiController, Route("api/mobil/notebook-oturumu")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme), EnableRateLimiting("mobil")]
public sealed class MobilNotebookOturumuController(UygulamaDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task Baglan([FromQuery] int? notebookId, CancellationToken ct)
    {
        if (!HttpContext.WebSockets.IsWebSocketRequest)
        {
            Response.StatusCode = StatusCodes.Status426UpgradeRequired;
            return;
        }
        var kullanici = User.FindFirstValue(JwtRegisteredClaimNames.Sub)!;
        if (notebookId is { } id && !await db.Notebooklar.Okunabilir(kullanici).AnyAsync(n => n.Id == id, ct))
        {
            Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        var saniye = long.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Exp)!, CultureInfo.InvariantCulture);
        var kalan = DateTimeOffset.FromUnixTimeSeconds(saniye) - DateTimeOffset.UtcNow;
        if (kalan <= TimeSpan.Zero)
        {
            Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        var oturumId = Guid.NewGuid();
        using var omur = CancellationTokenSource.CreateLinkedTokenSource(ct);
        omur.CancelAfter(kalan < TimeSpan.FromMinutes(10) ? kalan : TimeSpan.FromMinutes(10));
        using var soket = await HttpContext.WebSockets.AcceptWebSocketAsync();
        var tampon = new byte[1024];
        try
        {
            await Yanit(soket, new { tur = "baglandi", oturumId, notebookId }, omur.Token);
            while (soket.State == WebSocketState.Open)
            {
                var boyut = 0;
                ValueWebSocketReceiveResult mesaj;
                using var bosSure = CancellationTokenSource.CreateLinkedTokenSource(omur.Token);
                bosSure.CancelAfter(TimeSpan.FromSeconds(75));
                do
                {
                    mesaj = await soket.ReceiveAsync(tampon.AsMemory(boyut), bosSure.Token);
                    if (mesaj.MessageType == WebSocketMessageType.Close)
                    {
                        await Kapat(soket, WebSocketCloseStatus.NormalClosure);
                        return;
                    }
                    boyut += mesaj.Count;
                    if (mesaj.MessageType != WebSocketMessageType.Text || (!mesaj.EndOfMessage && boyut == tampon.Length))
                    {
                        await Kapat(soket, WebSocketCloseStatus.InvalidMessageType);
                        return;
                    }
                } while (!mesaj.EndOfMessage);

                if (!Ping(tampon.AsMemory(0, boyut)))
                {
                    await Kapat(soket, WebSocketCloseStatus.PolicyViolation);
                    return;
                }
                await Yanit(soket, new { tur = "pong", oturumId }, omur.Token);
            }
        }
        catch (OperationCanceledException) { soket.Abort(); }
        catch (WebSocketException) { soket.Abort(); }
    }

    private static bool Ping(ReadOnlyMemory<byte> veri)
    {
        try
        {
            using var belge = JsonDocument.Parse(veri);
            return belge.RootElement.ValueKind == JsonValueKind.Object &&
                belge.RootElement.TryGetProperty("tur", out var tur) && tur.ValueKind == JsonValueKind.String && tur.ValueEquals("ping");
        }
        catch (JsonException) { return false; }
    }

    private static ValueTask Yanit(WebSocket soket, object veri, CancellationToken ct) =>
        soket.SendAsync(JsonSerializer.SerializeToUtf8Bytes(veri).AsMemory(), WebSocketMessageType.Text, true, ct);

    private static async Task Kapat(WebSocket soket, WebSocketCloseStatus durum)
    {
        using var sure = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await soket.CloseOutputAsync(durum, null, sure.Token);
    }
}
