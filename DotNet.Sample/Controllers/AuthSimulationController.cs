using Microsoft.AspNetCore.Mvc;

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

        [HttpPost("login")]
        public IActionResult Login([FromForm] string username, [FromForm] string password)
        {
            if (username == "admin" && password == "password")
                return Ok(new { token = "fake-jwt-token" });
            return Unauthorized();
        }
    }
}
