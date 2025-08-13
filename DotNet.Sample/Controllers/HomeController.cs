using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DotNet.Sample.Controllers
{
    [ApiController]
    [Route("/")]
    public class HomeController : ControllerBase
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        [HttpGet]
        [AllowAnonymous]
        public string Index()
        {
            string instanceId = Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID") ?? "null";

            _logger.LogInformation("Home page accessed at {Time}", DateTime.Now);

            return $"Welcome to the DotNet Sample Application! - with Instance ID {instanceId}";
        }
    }
}
