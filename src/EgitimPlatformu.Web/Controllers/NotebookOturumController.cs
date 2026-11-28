using EgitimPlatformu.Web.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace EgitimPlatformu.Web.Controllers;

// Mobil uygulamanın notebook hücrelerini çalıştırdığı WebSocket ucu.
// Kimlik doğrulaması el sıkışmadaki "Authorization: Bearer" başlığıyla yapılır.
[ApiController, Route("api/notebook/oturum"), EnableRateLimiting("mobil")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class NotebookOturumController(NotebookOturumu oturum) : ControllerBase
{
    /// <summary>WebSocket oturumu açar. Mesaj sözleşmesi docs/NOTEBOOK_OTURUMU.md dosyasındadır.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status101SwitchingProtocols)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Ac()
    {
        if (!HttpContext.WebSockets.IsWebSocketRequest)
            return BadRequest(new { mesaj = "Bu uç yalnızca WebSocket bağlantısı kabul eder." });

        using var soket = await HttpContext.WebSockets.AcceptWebSocketAsync();
        try
        {
            await oturum.YurutAsync(soket, HttpContext.RequestAborted);
        }
        catch (OperationCanceledException) when (HttpContext.RequestAborted.IsCancellationRequested)
        {
            // İstemci bağlantıyı kapattı.
        }
        return new EmptyResult();
    }
}
