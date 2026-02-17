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
    public async Task<IActionResult> GetAll()
    {
        var items = await _db.Filaments
            .Include(x => x.MaterialType)
            .Include(x => x.Color)
            .Where(x => x.IsActive)
            .Where(x => x.MaterialType.IsActive)
            .Where(x => x.Color.IsActive)
            // Show only filaments that have at least one spool with material in it.
            .Where(x => x.Spools.Any(s => s.RemainingGrams > 0))
            .OrderBy(x => x.MaterialType.Name)
            .ThenBy(x => x.Color.Name)
            .ThenBy(x => x.Brand)
            .Select(x => new
            {
                x.Id,
                x.Brand,
                MaterialType = new
                {
                    MaterialTypeId = x.MaterialTypeId,
                    Name = x.MaterialType.Name
                },
                Color = new
                {
                    ColorId = x.ColorId,
                    Name = x.Color.Name,
                    x.Color.Hex
                }
            })
            .ToListAsync();

        return Ok(items);
    }
}
