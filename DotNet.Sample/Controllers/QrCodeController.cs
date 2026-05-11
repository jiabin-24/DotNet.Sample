using System.Collections.Concurrent;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using QRCoder;

namespace DotNet.Sample.Controllers;

[Route("QrCode")]
public class QrCodeController : Controller
{
    private static readonly ConcurrentDictionary<string, QrLoginSession> Sessions = new();
    private static readonly TimeSpan SessionLifetime = TimeSpan.FromMinutes(3);
    private readonly AzureAdSettings _azureAd;
    private readonly string _qrCodeHost;
    private readonly IConfigurationManager<OpenIdConnectConfiguration> _configManager;

    public QrCodeController(IConfiguration configuration)
    {
        var section = configuration.GetSection("AzureAd");
        _azureAd = new AzureAdSettings
        {
            Instance = section["Instance"] ?? throw new InvalidOperationException("AzureAd:Instance is not configured."),
            TenantId = section["TenantId"] ?? throw new InvalidOperationException("AzureAd:TenantId is not configured."),
            ClientId = section["ClientId"] ?? throw new InvalidOperationException("AzureAd:ClientId is not configured."),
            Audience = section["Audience"]
        };

        _qrCodeHost = configuration.GetSection("QrCode")["Host"] ?? throw new InvalidOperationException("QrCode:Host is not configured.");

        var metadataAddress = $"{_azureAd.Instance.TrimEnd('/')}/{_azureAd.TenantId.TrimEnd('/')}/v2.0/.well-known/openid-configuration";
        _configManager = new ConfigurationManager<OpenIdConnectConfiguration>(
            metadataAddress,
            new OpenIdConnectConfigurationRetriever(),
            new HttpDocumentRetriever { RequireHttps = true });
    }

    [HttpGet("")]
    public IActionResult Index()
    {
        return RedirectToAction(nameof(Login));
    }

    [HttpGet("login")]
    public IActionResult Login()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction(nameof(Profile));
        }

        CleanupExpiredSessions();

        var token = Guid.NewGuid().ToString("N");
        var expiresAt = DateTimeOffset.UtcNow.Add(SessionLifetime);

        Sessions[token] = new QrLoginSession
        {
            Token = token,
            ExpiresAt = expiresAt,
            Status = QrLoginStatus.Pending
        };

        var mobileUrl = $"{Request.Scheme}://{_qrCodeHost}/QrCode/token-receive?session={token}";

        var model = new QrCodeLoginViewModel
        {
            Token = token,
            QrCodeBase64 = BuildQrCode(mobileUrl),
            ExpiresAtUnix = expiresAt.ToUnixTimeMilliseconds(),
            MobileUrl = mobileUrl
        };

        return View("Login", model);
    }

    [HttpGet("token-receive")]
    public async Task<IActionResult> TokenReceive(
        [FromQuery(Name = "session")] string sessionToken,
        [FromQuery(Name = "access_token")] string? accessToken,
        [FromQuery(Name = "token")] string? token)
    {
        var upstreamToken = string.IsNullOrWhiteSpace(accessToken) ? token : accessToken;
        return await CompleteTokenLoginAsync(sessionToken, upstreamToken, null);
    }

    [HttpPost("token-receive")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> TokenReceivePost(
        [FromForm(Name = "session")] string sessionToken,
        [FromForm(Name = "access_token")] string? accessToken,
        [FromForm(Name = "token")] string? token,
        [FromHeader(Name = "Authorization")] string? authorization)
    {
        var upstreamToken = string.IsNullOrWhiteSpace(accessToken) ? token : accessToken;
        return await CompleteTokenLoginAsync(sessionToken, upstreamToken, authorization);
    }


    private async Task<IActionResult> CompleteTokenLoginAsync(string sessionToken, string? providedToken, string? authorization)
    {
        if (string.IsNullOrWhiteSpace(sessionToken) || !Sessions.TryGetValue(sessionToken, out var session))
        {
            return Content("二维码无效或已失效。", "text/plain", Encoding.UTF8);
        }

        if (session.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            Sessions.TryRemove(sessionToken, out _);
            return Content("二维码已过期，请重新发起登录。", "text/plain", Encoding.UTF8);
        }

        var upstreamToken = ResolveIncomingToken(providedToken, authorization);
        if (string.IsNullOrWhiteSpace(upstreamToken))
        {
            session.Status = QrLoginStatus.Failed;
            session.ErrorMessage = "未收到 token。";
            return Content("缺少 token。请通过 access_token/token 参数或 Authorization: Bearer 传入。", "text/plain", Encoding.UTF8);
        }

        try
        {
            var principal = await ValidateUpstreamTokenAsync(upstreamToken);
            session.Status = QrLoginStatus.Approved;
            session.UserName =
                principal.FindFirst("name")?.Value ??
                principal.FindFirst("preferred_username")?.Value ??
                principal.FindFirst(ClaimTypes.Email)?.Value ??
                principal.FindFirst(ClaimTypes.Name)?.Value ??
                "EntraUser";

            var page = "<html><head><meta charset=\"utf-8\" /></head><body style=\"font-family:Segoe UI,Microsoft YaHei,sans-serif;padding:24px;\">Token 校验成功，桌面端将自动完成登录。</body></html>";
            return Content(page, "text/html", Encoding.UTF8);
        }
        catch (Exception ex)
        {
            session.Status = QrLoginStatus.Failed;
            session.ErrorMessage = ex.Message;
            return Content($"token 校验失败: {ex.Message}", "text/plain", Encoding.UTF8);
        }
    }

    private static string? ResolveIncomingToken(string? providedToken, string? authorization)
    {
        if (!string.IsNullOrWhiteSpace(providedToken))
        {
            return providedToken;
        }

        if (string.IsNullOrWhiteSpace(authorization))
        {
            return null;
        }

        const string bearer = "Bearer ";
        return authorization.StartsWith(bearer, StringComparison.OrdinalIgnoreCase)
            ? authorization[bearer.Length..].Trim()
            : null;
    }

    [HttpGet("poll")]
    public async Task<IActionResult> Poll([FromQuery] string token)
    {
        if (string.IsNullOrWhiteSpace(token) || !Sessions.TryGetValue(token, out var session))
        {
            return Ok(new { status = "invalid" });
        }

        if (session.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            Sessions.TryRemove(token, out _);
            return Ok(new { status = "expired" });
        }

        if (session.Status == QrLoginStatus.Failed)
        {
            return Ok(new { status = "failed", message = session.ErrorMessage ?? "移动端登录失败，请重试。" });
        }

        if (session.Status != QrLoginStatus.Approved)
        {
            return Ok(new { status = "pending" });
        }

        if (!session.DesktopSignedIn)
        {
            var userName = string.IsNullOrWhiteSpace(session.UserName) ? "MobileUser" : session.UserName;
            var claims = new List<Claim>
            {
                new(ClaimTypes.Name, userName),
                new("auth_type", "teams_qr")
            };
            var identity = new ClaimsIdentity(claims, "QrCookie");
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(
                "QrCookie",
                principal,
                new AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(30)
                });

            session.DesktopSignedIn = true;
        }

        return Ok(new { status = "authenticated", redirectUrl = Url.Action(nameof(Profile)) ?? "/QrCode/profile" });
    }

    [Authorize(AuthenticationSchemes = "QrCookie")]
    [HttpGet("profile")]
    public IActionResult Profile()
    {
        var userName = User.Identity?.Name ?? "Unknown";
        return Content($"扫码登录成功，当前用户：{userName}", "text/plain", Encoding.UTF8);
    }

    [Authorize(AuthenticationSchemes = "QrCookie")]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync("QrCookie");
        return RedirectToAction(nameof(Login));
    }

    private static string BuildQrCode(string value)
    {
        using var qrGenerator = new QRCodeGenerator();
        using var qrData = qrGenerator.CreateQrCode(value, QRCodeGenerator.ECCLevel.Q);
        var pngQrCode = new PngByteQRCode(qrData);
        var bytes = pngQrCode.GetGraphic(12);
        return Convert.ToBase64String(bytes);
    }

    private static void CleanupExpiredSessions()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var item in Sessions)
        {
            if (item.Value.ExpiresAt <= now)
            {
                Sessions.TryRemove(item.Key, out _);
            }
        }
    }

    private async Task<ClaimsPrincipal> ValidateUpstreamTokenAsync(string token)
    {
        var config = await _configManager.GetConfigurationAsync(HttpContext.RequestAborted);
        var handler = new JwtSecurityTokenHandler();

        var audiences = new List<string> { _azureAd.ClientId };
        if (!string.IsNullOrWhiteSpace(_azureAd.Audience))
        {
            audiences.Add(_azureAd.Audience);
        }

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuers = new[]
            {
                config.Issuer,
                $"{_azureAd.Instance.TrimEnd('/')}/{_azureAd.TenantId.TrimEnd('/')}/v2.0",
                $"https://sts.windows.net/{_azureAd.TenantId.TrimEnd('/')}/"
            },
            ValidateAudience = true,
            ValidAudiences = audiences.Distinct(StringComparer.OrdinalIgnoreCase),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(2),
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = config.SigningKeys
        };

        return handler.ValidateToken(token, validationParameters, out _);
    }

    private sealed class QrLoginSession
    {
        public string Token { get; init; } = string.Empty;
        public DateTimeOffset ExpiresAt { get; init; }
        public QrLoginStatus Status { get; set; }
        public string? UserName { get; set; }
        public string? ErrorMessage { get; set; }
        public bool DesktopSignedIn { get; set; }
    }

    private sealed class AzureAdSettings
    {
        public string Instance { get; init; } = string.Empty;
        public string TenantId { get; init; } = string.Empty;
        public string ClientId { get; init; } = string.Empty;
        public string? Audience { get; init; }
    }

}

public sealed class QrCodeLoginViewModel
{
    public string Token { get; init; } = string.Empty;
    public string QrCodeBase64 { get; init; } = string.Empty;
    public long ExpiresAtUnix { get; init; }
    public string MobileUrl { get; init; } = string.Empty;
}

public enum QrLoginStatus
{
    Pending = 0,
    Approved = 1,
    Failed = 2
}
