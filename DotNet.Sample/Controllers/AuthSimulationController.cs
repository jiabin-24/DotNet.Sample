using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace DotNet.Sample.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class AuthSimulationController : ControllerBase
    {
        private readonly AzureAdSettings _azureAd;
        private readonly IConfigurationManager<OpenIdConnectConfiguration> _configManager;

        public AuthSimulationController(IConfiguration configuration)
        {
            var section = configuration.GetSection("AzureAd");
            _azureAd = new AzureAdSettings
            {
                Instance = section["Instance"] ?? throw new InvalidOperationException("AzureAd:Instance is not configured."),
                TenantId = section["TenantId"] ?? throw new InvalidOperationException("AzureAd:TenantId is not configured."),
                ClientId = section["ClientId"] ?? throw new InvalidOperationException("AzureAd:ClientId is not configured."),
                Audience = section["Audience"]
            };

            var metadataAddress = $"{_azureAd.Instance.TrimEnd('/')}/{_azureAd.TenantId.TrimEnd('/')}/v2.0/.well-known/openid-configuration";
            _configManager = new ConfigurationManager<OpenIdConnectConfiguration>(
                metadataAddress,
                new OpenIdConnectConfigurationRetriever(),
                new HttpDocumentRetriever { RequireHttps = true });
        }

        [HttpGet("public")]
        public IActionResult Public() => Ok(new { message = "public endpoint" });

        [HttpGet("protected")]
        public IActionResult Protected() => Unauthorized(new { message = "unauthorized - simulate protected" });

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme + ",QrCookie")]
        [HttpGet("authorized")]
        public IActionResult Authorized()
        {
            var isAuthenticated = HttpContext.User.Identity?.IsAuthenticated;
            var userName =
                HttpContext.User.FindFirst("name")?.Value ??
                HttpContext.User.FindFirst(ClaimTypes.Name)?.Value ??
                HttpContext.User.FindFirst("preferred_username")?.Value ??
                "Unknown";

            var passHeader = HttpContext.Request.Headers;

            return Ok($"Hello {userName}!");
        }

        [HttpPost("login")]
        public IActionResult Login([FromForm] string username, [FromForm] string password)
        {
            return BadRequest(new
            {
                message = "Local login is disabled. Use Microsoft Entra ID to acquire an access_token for this API."
            });
        }

        [HttpGet("/token-login/{*redirectPath}")]
        public async Task<IActionResult> TokenLogin(
            [FromRoute] string? redirectPath,
            [FromQuery(Name = "token")] string? token,
            [FromQuery(Name = "access_token")] string? accessToken,
            [FromHeader(Name = "Authorization")] string? authorization)
        {
            var upstreamToken = ResolveIncomingToken(string.IsNullOrWhiteSpace(accessToken) ? token : accessToken, authorization);
            if (string.IsNullOrWhiteSpace(upstreamToken))
            {
                return BadRequest(new { message = "Missing token. Pass via token/access_token query or Authorization: Bearer." });
            }

            try
            {
                var principal = await ValidateUpstreamTokenAsync(upstreamToken);
                var userName =
                    principal.FindFirst("name")?.Value ??
                    principal.FindFirst("preferred_username")?.Value ??
                    principal.FindFirst(ClaimTypes.Email)?.Value ??
                    principal.FindFirst(ClaimTypes.Name)?.Value ??
                    "EntraUser";

                var claims = new List<Claim>(principal.Claims);
                if (!claims.Any(c => c.Type == ClaimTypes.Name))
                {
                    claims.Add(new Claim(ClaimTypes.Name, userName));
                }
                claims.Add(new Claim("auth_type", "token_login"));

                var identity = new ClaimsIdentity(claims, "QrCookie", ClaimTypes.Name, ClaimTypes.Role);
                await HttpContext.SignInAsync(
                    "QrCookie",
                    new ClaimsPrincipal(identity),
                    new AuthenticationProperties
                    {
                        IsPersistent = true,
                        ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(30)
                    });

                return Redirect(NormalizeRedirectPath(redirectPath));
            }
            catch (Exception ex)
            {
                return Unauthorized(new { message = $"token validation failed: {ex.Message}" });
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

        private static string NormalizeRedirectPath(string? redirectPath)
        {
            var decoded = string.IsNullOrWhiteSpace(redirectPath) ? "/" : Uri.UnescapeDataString(redirectPath);

            if (decoded.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                decoded.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return "/";
            }

            if (!decoded.StartsWith('/'))
            {
                decoded = "/" + decoded;
            }

            return decoded;
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

        private sealed class AzureAdSettings
        {
            public string Instance { get; init; } = string.Empty;
            public string TenantId { get; init; } = string.Empty;
            public string ClientId { get; init; } = string.Empty;
            public string? Audience { get; init; }
        }
    }
}
