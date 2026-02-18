using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrintIt.Infrastructure.Persistence;

namespace PrintIt.Api.Controllers;

[ApiController]
[Route("api/v1/filaments")]
public class FilamentsController : ControllerBase
{
    private readonly AppDbContext _db;

    public FilamentsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetActive()
    {
        var items = await _db.Filaments
            .AsNoTracking()
            // Keep explicit active filter even if there is a global query filter (clearer + safer)
            .Where(f => f.IsActive)
            // In stock: at least one spool with RemainingGrams > 0
            .Where(f => f.Spools.Any(s => s.RemainingGrams > 0))
            .OrderBy(f => f.Brand)
            .ThenBy(f => f.MaterialType.Name)
            .ThenBy(f => f.Color.Name)
            .Select(f => new
            {
                f.Id,
                f.Brand,
                MaterialType = new
                {
                    f.MaterialTypeId,
                    Name = f.MaterialType.Name
                },
                Color = new
                {
                    f.ColorId,
                    Name = f.Color.Name,
                    f.Color.Hex
                },
                Inventory = new
                {
                    TotalRemainingGrams = f.Spools
                        .Where(s => s.RemainingGrams > 0)
                        .Sum(s => s.RemainingGrams),
                    AvailableSpools = f.Spools.Count(s => s.RemainingGrams > 0)
                }
            })
            .ToListAsync();

        return Ok(items);
    }
}
