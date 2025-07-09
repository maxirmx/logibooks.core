using Microsoft.AspNetCore.Mvc;

using Logibooks.Core.Services;
using Logibooks.Core.Data;

namespace Logibooks.Core.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CountryCodesController(
    IHttpContextAccessor httpContextAccessor,
    AppDbContext db,
    UpdateCountryCodesService service,
    ILogger<CountryCodesController> logger) : LogibooksControllerBase(httpContextAccessor, db, logger)
{
    private readonly UpdateCountryCodesService _service = service;

    [HttpPost("update")] 
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Update()
    {
        await _service.RunAsync();
        return NoContent();
    }
}
