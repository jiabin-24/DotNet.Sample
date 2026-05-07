using System.Collections.Concurrent;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QRCoder;

namespace DotNet.Sample.Controllers;

[Route("QrCode")]
public class QrCodeController : Controller
{
    private static readonly ConcurrentDictionary<string, QrLoginSession> Sessions = new();
    private static readonly TimeSpan SessionLifetime = TimeSpan.FromMinutes(3);

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

        var url = Url.ActionLink(nameof(MobileScan), values: new { token });
        var mobileUrl =  $"{Request.Scheme}://local.niuai.cc/QrCode/mobile-scan?token={token}";

        var model = new QrCodeLoginViewModel
        {
            Token = token,
            QrCodeBase64 = BuildQrCode(mobileUrl),
            ExpiresAtUnix = expiresAt.ToUnixTimeMilliseconds()
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
            ExpiresAt = session.ExpiresAt.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss")
        };

        return View("MobileScan", model);
    }

    [HttpPost("mobile-confirm")]
    public IActionResult MobileConfirm([FromForm] string token, [FromForm] string? userName)
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

        session.Status = QrLoginStatus.Approved;
        session.UserName = string.IsNullOrWhiteSpace(userName) ? "MobileUser" : userName.Trim();

        return Content("已确认登录，桌面端将自动进入登录状态。", "text/plain", Encoding.UTF8);
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
                new("auth_type", "qr_code")
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

    private sealed class QrLoginSession
    {
        public string Token { get; init; } = string.Empty;
        public DateTimeOffset ExpiresAt { get; init; }
        public QrLoginStatus Status { get; set; }
        public string? UserName { get; set; }
        public bool DesktopSignedIn { get; set; }
    }
}

public sealed class QrCodeLoginViewModel
{
    public string Token { get; init; } = string.Empty;
    public string QrCodeBase64 { get; init; } = string.Empty;
    public long ExpiresAtUnix { get; init; }
}

public sealed class MobileScanViewModel
{
    public string Token { get; init; } = string.Empty;
    public string ExpiresAt { get; init; } = string.Empty;
}

public enum QrLoginStatus
{
    Pending = 0,
    Approved = 1
}
