
using Microsoft.AspNetCore.Mvc;

namespace Logibooks.Core.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UserController : ControllerBase
    {
        [HttpGet]
        public IActionResult Get() => Ok(new { Message = "Hello from Logibooks API!" });
    }
}
