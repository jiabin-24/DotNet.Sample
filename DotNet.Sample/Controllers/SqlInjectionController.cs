using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Linq;

namespace DotNet.Sample.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class SqlInjectionController : ControllerBase
    {
        private static readonly List<(int Id, string Name)> _items = new()
        {
            (1, "Alice"), (2, "Bob"), (3, "Carol")
        };

        [HttpGet("find")]
        public IActionResult Find([FromQuery] string name)
        {
            // Simulate unsafe SQL concatenation by performing a naive search
            var results = _items.Where(i => ("'" + i.Name + "'").Contains(name ?? string.Empty)).ToList();
            return Ok(results);
        }
    }
}
