using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrintIt.Infrastructure.Persistence;

namespace PrintIt.Api.Controllers;

[ApiController]
[Route("api/v1/material-types")]
public class MaterialTypesController : ControllerBase
{
    private readonly AppDbContext _db;

    // DbContext is injected by DI, so the controller can query the database.
    public MaterialTypesController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetActive()
    {
        // Return only active items for the public API.
        var items = await _db.MaterialTypes
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new
            {
                x.Id,
                x.Name
            })
            .ToListAsync();

        return Ok(items);
    }



}
