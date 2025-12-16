using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace DotNet.Sample.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class InputValidationController : ControllerBase
    {
        public class Model
        {
            [Required]
            [StringLength(10, MinimumLength = 3)]
            public string Name { get; set; }

            [Range(1, 120)]
            public int Age { get; set; }
        }

        [HttpPost("validate")]
        public IActionResult Validate([FromBody] Model model)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            return Ok(model);
        }
    }
}
