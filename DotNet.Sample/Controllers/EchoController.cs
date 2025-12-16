using Microsoft.AspNetCore.Mvc;

namespace DotNet.Sample.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class EchoController : ControllerBase
    {
        [HttpGet]
        public IActionResult Get([FromQuery] string input)
        {
            return Ok(new { echoed = input });
        }

        public class EchoRequest { public string Input { get; set; } }

        [HttpPost]
        public IActionResult Post([FromBody] EchoRequest req)
        {
            return Ok(new { echoed = req?.Input });
        }
    }
}
