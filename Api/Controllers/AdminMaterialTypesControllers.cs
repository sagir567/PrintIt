using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrintIt.Domain.Entities;
using PrintIt.Infrastructure.Persistence;

namespace PrintIt.Api.Controllers;

[ApiController]
[Route("api/v1/admin/material-types")]
public class AdminMaterialTypesController : ControllerBase
{
    private readonly AppDbContext _db;

    // DbContext is injected so we can write to the database.
    public AdminMaterialTypesController(AppDbContext db)
    {
        _db = db;
    }

    public record CreateMaterialTypeRequest(string Name);


    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateMaterialTypeRequest request)
    {
        var name = (request.Name ?? string.Empty).Trim();
    
        if (name.Length == 0)
            return BadRequest(new { message = "Name is required." });
    
        if (name.Length > 50)
            return BadRequest(new { message = "Name must be 50 characters or less." });
    
        var existing = await _db.MaterialTypes
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Name == name);
    
        if (existing != null)
        {
            if (existing.IsActive)
                return Conflict(new { message = "Material type already exists." });
    
            existing.IsActive = true;
            await _db.SaveChangesAsync();
    
            return Ok(new { existing.Id, existing.Name, existing.IsActive });
        }
    
        var entity = new MaterialType
        {
            Name = name,
            IsActive = true
        };
    
        _db.MaterialTypes.Add(entity);
    
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return Conflict(new { message = "Material type already exists." });
        }
    
        return Created($"/api/v1/material-types/{entity.Id}", new { entity.Id, entity.Name, entity.IsActive });
    }


    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var entity = await _db.MaterialTypes.FindAsync(id); 
        if (entity == null)
            return NotFound();  
        _db.MaterialTypes.Remove(entity);
        await _db.SaveChangesAsync();   
        return NoContent();
    }

    // when chaging existing items, we prefer "soft delete" (deactivation) over hard delete, to keep data integrity and history.
    // this endpoint is for admin use only, so we don't return the item data (and we don't care if it was already inactive).
    // also since we change the state of the item and not replace it's better to use PATCH and not PUT.
    [HttpPatch("{id}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid id)
    {
        var entity = await _db.MaterialTypes
        .IgnoreQueryFilters()
        .FirstOrDefaultAsync(x => x.Id == id);


        if (entity == null)
            return NotFound();

        if (!entity.IsActive)
            return NoContent(); // already inactive

        entity.IsActive = false;
        await _db.SaveChangesAsync();

        return NoContent();
    }

    [HttpPatch("{id}/activate")]
    public async Task<IActionResult> Activate(Guid id)
    {
        var entity = await _db.MaterialTypes
        .IgnoreQueryFilters()
        .FirstOrDefaultAsync(x => x.Id == id);

        if (entity == null)
            return NotFound();

        if (entity.IsActive)
            return NoContent();

        entity.IsActive = true;
        await _db.SaveChangesAsync();

        return NoContent();
    }


    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var items = await _db.MaterialTypes
            .IgnoreQueryFilters()
            .OrderBy(x => x.Name)
            .Select(x => new { x.Id, x.Name, x.IsActive })
            .ToListAsync();
    
        return Ok(items);
    }
}
