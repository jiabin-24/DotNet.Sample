using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using System.Threading.Tasks;

namespace DotNet.Sample.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class EchoController : ControllerBase
    {
        private static readonly HttpClient _httpClient = new HttpClient();

        [HttpGet]
        public IActionResult Get()
        {
            return Ok(new { echoed = "input" });
        }

        public class EchoRequest { public string Input { get; set; } }

        [HttpPost]
        public IActionResult Post([FromBody] EchoRequest req)
        {
            return Ok(new { echoed = req?.Input });
        }

        //[HttpGet("Send")]
        //public async Task<IActionResult> SendHttpRequest()
        //{
        //    try
        //    {
        //        var content = await _httpClient.GetStringAsync("https://20.187.72.127/Health");
        //        return Ok(new { echoed = content });
        //    }
        //    catch (HttpRequestException ex)
        //    {
        //        return StatusCode(502, new { error = ex.Message });
        //    }
        //}

        //[HttpPost("Get")]
        //public async Task<IActionResult> GetHttpRequest()
        //{
        //    try
        //    {
        //        return Ok(new { echoed = "Get" });
        //    }
        //    catch (HttpRequestException ex)
        //    {
        //        return StatusCode(502, new { error = ex.Message });
        //    }
        //}
    }
}
