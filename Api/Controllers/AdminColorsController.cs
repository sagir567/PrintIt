using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrintIt.Domain.Entities;
using PrintIt.Infrastructure.Persistence;

namespace PrintIt.Api.Controllers;

[ApiController]
[Route("api/v1/admin/colors")]
public class AdminColorsController : ControllerBase
{
    private readonly AppDbContext _db;

    // DbContext is injected by DI so we can write to the database.
    public AdminColorsController(AppDbContext db)
    {
        _db = db;
    }

    public record CreateColorRequest(string Name, string? Hex);
[HttpPost]
public async Task<IActionResult> Create([FromBody] CreateColorRequest request)
{
    var name = (request.Name ?? string.Empty).Trim();
    var hex = (request.Hex ?? string.Empty).Trim();

    if (name.Length == 0)
        return BadRequest(new { message = "Name is required." });

    if (name.Length > 50)
        return BadRequest(new { message = "Name must be 50 characters or less." });

    if (hex.Length > 0)
    {
        if (hex.Length != 7 || hex[0] != '#')
            return BadRequest(new { message = "Hex must be in the format #RRGGBB." });
    }

    var existing = await _db.Colors
        .IgnoreQueryFilters()
        .FirstOrDefaultAsync(x => x.Name == name);

    if (existing != null)
    {
        if (existing.IsActive)
            return Conflict(new { message = "Color already exists." });

        existing.IsActive = true;
        existing.Hex = hex.Length == 0 ? null : hex;
        await _db.SaveChangesAsync();

        return Ok(new { existing.Id, existing.Name, existing.Hex, existing.IsActive });
    }

    var entity = new Color
    {
        Name = name,
        Hex = hex.Length == 0 ? null : hex,
        IsActive = true
    };

    _db.Colors.Add(entity);

    try
    {
        await _db.SaveChangesAsync();
    }
    catch (DbUpdateException)
    {
        return Conflict(new { message = "Color already exists." });
    }

    return Created($"/api/v1/colors/{entity.Id}", new { entity.Id, entity.Name, entity.Hex, entity.IsActive });
}


    [HttpPatch("{id}/deactivate")]
public async Task<IActionResult> Deactivate(Guid id)
{
    // Soft delete: deactivate instead of removing the row.
    var entity = await _db.Colors
        .IgnoreQueryFilters()
        .FirstOrDefaultAsync(x => x.Id == id);

    if (entity == null)
        return NotFound();

    if (!entity.IsActive)
        return NoContent();

    entity.IsActive = false;
    await _db.SaveChangesAsync();

    return NoContent();
}



    [HttpPatch("{id}/activate")]
    public async Task<IActionResult> Activate(Guid id)
    {
        var entity = await _db.Colors
        .IgnoreQueryFilters()
        .FirstOrDefaultAsync(x=> x.Id ==id);
        
        if (entity == null)return NotFound();
        if(entity.IsActive)return NoContent();

        entity.IsActive = true;
        await _db.SaveChangesAsync();
        return NoContent();
    }

}
