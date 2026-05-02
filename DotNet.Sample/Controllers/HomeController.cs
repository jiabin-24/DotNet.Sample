using Microsoft.AspNetCore.Mvc;

namespace DotNet.Sample.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class HomeController : ControllerBase
    {
        [HttpGet(Name = "Home")]
        public async Task<IActionResult> Get()
        {
            return Ok($"Hello World!");
        }
    }
}
