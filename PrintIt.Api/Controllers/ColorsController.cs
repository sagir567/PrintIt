using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrintIt.Infrastructure.Persistence;

namespace PrintIt.Api.Controllers;

[ApiController]
[Route("api/v1/colors")]
public class ColorsController : ControllerBase
{
    private readonly AppDbContext _db;

    // DbContext is injected by DI so we can query the database.
    public ColorsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetActive()
    {
        // Public endpoint: return only active colors.
        var items = await _db.Colors
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Hex
            })
            .ToListAsync();

        return Ok(items);
    }
}
