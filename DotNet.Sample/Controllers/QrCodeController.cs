using System.Collections.Concurrent;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
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
    private readonly IConfigurationManager<OpenIdConnectConfiguration> _configManager;

    public QrCodeController(IConfiguration configuration)
    {
        var section = configuration.GetSection("AzureAd");
        _azureAd = new AzureAdSettings
        {
            Instance = section["Instance"] ?? throw new InvalidOperationException("AzureAd:Instance is not configured."),
            TenantId = section["TenantId"] ?? throw new InvalidOperationException("AzureAd:TenantId is not configured."),
            ClientId = section["ClientId"] ?? throw new InvalidOperationException("AzureAd:ClientId is not configured.")
        };

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
            Status = QrLoginStatus.Pending,
            Nonce = Guid.NewGuid().ToString("N")
        };

        var mobileUrl = $"{Request.Scheme}://local.niuai.cc/QrCode/mobile-scan?token={token}";

        var model = new QrCodeLoginViewModel
        {
            Token = token,
            QrCodeBase64 = BuildQrCode(mobileUrl),
            ExpiresAtUnix = expiresAt.ToUnixTimeMilliseconds(),
            MobileUrl = mobileUrl
        };

        return View("Login", model);
    }

    [HttpGet("mobile-scan")]
    public IActionResult MobileScan([FromQuery] string token)
    {
        if (string.IsNullOrWhiteSpace(token) || !Sessions.TryGetValue(token, out var session))
        {
            return Content("二维码无效或已失效。", "text/plain", Encoding.UTF8);
        }

        if (session.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            Sessions.TryRemove(token, out _);
            return Content("二维码已过期，请重新发起登录。", "text/plain", Encoding.UTF8);
        }

        var model = new MobileScanViewModel
        {
            Token = token,
            ExpiresAt = session.ExpiresAt.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss"),
            AuthorizeUrl = BuildAuthorizeUrl(token, session.Nonce)
        };

        return View("MobileScan", model);
    }

    [HttpPost("teams-callback")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> TeamsCallback([FromForm] string? state, [FromForm] string? id_token, [FromForm] string? error, [FromForm] string? error_description)
    {
        if (!string.IsNullOrWhiteSpace(error))
        {
            return Content($"Teams/Entra 登录失败: {error}. {error_description}", "text/plain", Encoding.UTF8);
        }

        if (string.IsNullOrWhiteSpace(state) || !Sessions.TryGetValue(state, out var session))
        {
            return Content("二维码无效或已失效。", "text/plain", Encoding.UTF8);
        }

        if (session.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            Sessions.TryRemove(state, out _);
            return Content("二维码已过期，请重新发起登录。", "text/plain", Encoding.UTF8);
        }

        if (string.IsNullOrWhiteSpace(id_token))
        {
            session.Status = QrLoginStatus.Failed;
            session.ErrorMessage = "未收到 id_token，无法完成登录。";
            return Content("登录回调缺少 id_token。", "text/plain", Encoding.UTF8);
        }

        try
        {
            var principal = await ValidateIdTokenAsync(id_token, session.Nonce);

            session.Status = QrLoginStatus.Approved;
            session.UserName =
                principal.FindFirst("name")?.Value ??
                principal.FindFirst("preferred_username")?.Value ??
                principal.FindFirst(ClaimTypes.Email)?.Value ??
                "TeamsUser";

            var page = "<html><head><meta charset=\"utf-8\" /></head><body style=\"font-family:Segoe UI,Microsoft YaHei,sans-serif;padding:24px;\">Teams 扫码登录成功，请返回桌面端继续。</body></html>";
            return Content(page, "text/html", Encoding.UTF8);
        }
        catch (Exception ex)
        {
            session.Status = QrLoginStatus.Failed;
            session.ErrorMessage = ex.Message;
            return Content($"登录校验失败: {ex.Message}", "text/plain", Encoding.UTF8);
        }
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

    private string BuildAuthorizeUrl(string state, string nonce)
    {
        var redirectUri = Url.ActionLink(nameof(TeamsCallback), values: null, protocol: Request.Scheme, host: Request.Host.ToString())
            ?? $"{Request.Scheme}://{Request.Host}/QrCode/teams-callback";
        var authorizeEndpoint = $"{_azureAd.Instance.TrimEnd('/')}/{_azureAd.TenantId.TrimEnd('/')}/oauth2/v2.0/authorize";

        var query = new Dictionary<string, string?>
        {
            ["client_id"] = _azureAd.ClientId,
            ["response_type"] = "id_token",
            ["redirect_uri"] = redirectUri,
            ["response_mode"] = "form_post",
            ["scope"] = "openid profile email",
            ["state"] = state,
            ["nonce"] = nonce,
            ["prompt"] = "select_account"
        };

        return QueryHelpers.AddQueryString(authorizeEndpoint, query);
    }

    private async Task<ClaimsPrincipal> ValidateIdTokenAsync(string idToken, string expectedNonce)
    {
        var config = await _configManager.GetConfigurationAsync(HttpContext.RequestAborted);
        var handler = new JwtSecurityTokenHandler();
        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuers = new[]
            {
                config.Issuer,
                $"{_azureAd.Instance.TrimEnd('/')}/{_azureAd.TenantId.TrimEnd('/')}/v2.0"
            },
            ValidateAudience = true,
            ValidAudience = _azureAd.ClientId,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(2),
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = config.SigningKeys
        };

        var principal = handler.ValidateToken(idToken, validationParameters, out _);
        var nonce = principal.FindFirst("nonce")?.Value;
        if (!string.Equals(nonce, expectedNonce, StringComparison.Ordinal))
        {
            throw new SecurityTokenValidationException("nonce 校验失败。请重新扫码。");
        }

        return principal;
    }

    private sealed class QrLoginSession
    {
        public string Token { get; init; } = string.Empty;
        public DateTimeOffset ExpiresAt { get; init; }
        public QrLoginStatus Status { get; set; }
        public string Nonce { get; init; } = string.Empty;
        public string? UserName { get; set; }
        public string? ErrorMessage { get; set; }
        public bool DesktopSignedIn { get; set; }
    }

    private sealed class AzureAdSettings
    {
        public string Instance { get; init; } = string.Empty;
        public string TenantId { get; init; } = string.Empty;
        public string ClientId { get; init; } = string.Empty;
    }
}

public sealed class QrCodeLoginViewModel
{
    public string Token { get; init; } = string.Empty;
    public string QrCodeBase64 { get; init; } = string.Empty;
    public long ExpiresAtUnix { get; init; }
    public string MobileUrl { get; init; } = string.Empty;
}

public sealed class MobileScanViewModel
{
    public string Token { get; init; } = string.Empty;
    public string ExpiresAt { get; init; } = string.Empty;
    public string AuthorizeUrl { get; init; } = string.Empty;
}

public enum QrLoginStatus
{
    Pending = 0,
    Approved = 1,
    Failed = 2
}
