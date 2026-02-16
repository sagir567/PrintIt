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
            .Where(x => x.IsActive)
            .OrderBy(x => x.Brand)
            .Select(x => new
            {
                x.Id,
                x.Brand,
                MaterialType = new
                {
                    x.MaterialTypeId,
                    Name = x.MaterialType.Name
                },
                Color = new
                {
                    x.ColorId,
                    Name = x.Color.Name,
                    x.Color.Hex
                }
            })
            .ToListAsync();

        return Ok(items);
    }
}
