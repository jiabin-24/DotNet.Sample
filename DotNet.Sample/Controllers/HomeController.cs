using Microsoft.AspNetCore.Mvc;

namespace DotNet.Sample.Controllers
{
    [ApiController]
    [Route("/")]
    public class HomeController : ControllerBase
    {
        private static readonly HttpClient _httpClient = new HttpClient();

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            try
            {
                var content = await _httpClient.GetStringAsync("https://niuai-app-dev-apim.azure-api.net/WeatherForecast");
                return Ok(new { echoed = content });
            }
            catch (HttpRequestException ex)
            {
                return StatusCode(502, new { error = ex.Message });
            }
        }
    }
}
