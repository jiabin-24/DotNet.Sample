using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace DotNet.Sample.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class AuthSimulationController : ControllerBase
    {
        [HttpGet("public")]
        public IActionResult Public() => Ok(new { message = "public endpoint" });

        [HttpGet("protected")]
        public IActionResult Protected() => Unauthorized(new { message = "unauthorized - simulate protected" });

        [Authorize]
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
    }
}
