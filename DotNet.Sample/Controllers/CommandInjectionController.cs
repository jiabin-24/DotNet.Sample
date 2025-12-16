using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace DotNet.Sample.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class CommandInjectionController : ControllerBase
    {
        [HttpGet("ping")]
        public IActionResult Ping([FromQuery] string host)
        {
            // Intentionally naive command execution simulation for security testing only.
            // It executes system ping which may not be available in all environments.
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "ping",
                    Arguments = host ?? "127.0.0.1",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var p = Process.Start(psi);
                var output = p?.StandardOutput.ReadToEnd();
                p?.WaitForExit();
                return Ok(new { output });
            }
            catch (System.Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}
