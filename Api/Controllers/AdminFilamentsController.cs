using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrintIt.Domain.Entities;
using PrintIt.Infrastructure.Persistence;
using PrintIt.Domain.DomainLogic;



namespace PrintIt.Api.Controllers;

[ApiController]
[Route("api/v1/admin/filaments")]
public class AdminFilamentsController : ControllerBase
{
    private readonly AppDbContext _db;

    public AdminFilamentsController(AppDbContext db)
    {
        _db = db;
    }

    public record CreateFilamentRequest(
        Guid MaterialTypeId,
        Guid ColorId,
        string Brand,
        decimal CostPerKg
    );
[HttpPost]
public async Task<IActionResult> Create([FromBody] CreateFilamentRequest request)
{
    var brand = (request.Brand ?? string.Empty).Trim();

    if (brand.Length == 0)
        return BadRequest(new { message = "Brand is required." });

    if (brand.Length > 80)
        return BadRequest(new { message = "Brand must be 80 characters or less." });

    if (request.CostPerKg <= 0)
        return BadRequest(new { message = "CostPerKg must be greater than 0." });

    var materialType = await _db.MaterialTypes
        .IgnoreQueryFilters()
        .FirstOrDefaultAsync(x => x.Id == request.MaterialTypeId);

    if (materialType == null)
        return NotFound(new { message = "MaterialType not found." });

    if (!materialType.IsActive)
        return Conflict(new { message = "MaterialType is inactive." });

    var color = await _db.Colors
        .IgnoreQueryFilters()
        .FirstOrDefaultAsync(x => x.Id == request.ColorId);

    if (color == null)
        return NotFound(new { message = "Color not found." });

    if (!color.IsActive)
        return Conflict(new { message = "Color is inactive." });

    var existing = await _db.Filaments
        .IgnoreQueryFilters()
        .FirstOrDefaultAsync(x =>
            x.MaterialTypeId == request.MaterialTypeId &&
            x.ColorId == request.ColorId &&
            x.Brand == brand);

    if (existing != null)
    {
        if (existing.IsActive)
            return Conflict(new { message = "Filament already exists for this material, color, and brand." });

        existing.IsActive = true;
        existing.CostPerKg = request.CostPerKg;
        await _db.SaveChangesAsync();

        return Ok(new
        {
            existing.Id,
            existing.MaterialTypeId,
            existing.ColorId,
            existing.Brand,
            existing.CostPerKg,
            existing.IsActive
        });
    }

    var entity = new Filament
    {
        MaterialTypeId = request.MaterialTypeId,
        ColorId = request.ColorId,
        Brand = brand,
        CostPerKg = request.CostPerKg,
        IsActive = true
    };

    _db.Filaments.Add(entity);

    try
    {
        await _db.SaveChangesAsync();
    }
    catch (DbUpdateException)
    {
        return Conflict(new { message = "Filament already exists for this material, color, and brand." });
    }

    return Created($"/api/v1/admin/filaments/{entity.Id}", new
    {
        entity.Id,
        entity.MaterialTypeId,
        entity.ColorId,
        entity.Brand,
        entity.CostPerKg,
        entity.IsActive
    });
}

[HttpGet]
public async Task<IActionResult> GetAll()
{
    var items = await _db.Filaments
        .IgnoreQueryFilters()
        .OrderBy(x => x.Brand)
        .ThenBy(x => x.MaterialType.Name)
        .ThenBy(x => x.Color.Name)
        .Select(x => new
        {
            x.Id,
            x.Brand,
            x.CostPerKg,
            x.IsActive,
            x.CreatedAtUtc,
            MaterialType = new
            {
                x.MaterialTypeId,
                Name = x.MaterialType.Name,
                x.MaterialType.IsActive
            },
            Color = new
            {
                x.ColorId,
                Name = x.Color.Name,
                x.Color.Hex,
                x.Color.IsActive
            }
        })
        .ToListAsync();

    return Ok(items);
}


[HttpGet("{id}/spools")]
public async Task<IActionResult> GetSpools(Guid id)
{
    var filamentExists = await _db.Filaments
        .IgnoreQueryFilters()
        .AnyAsync(x => x.Id == id);

    if (!filamentExists)
        return NotFound(new { message = "Filament not found." });

    var spools = await _db.FilamentSpools
        .Where(x => x.FilamentId == id)
        .OrderByDescending(x => x.CreatedAtUtc)
        .Select(x => new
        {
            x.Id,
            x.InitialGrams,
            x.RemainingGrams,
            x.Status,
            x.CreatedAtUtc,
            x.LastUsedAtUtc
        })
        .ToListAsync();

    return Ok(spools);
}


    public record ConsumeFilamentRequest(int GramsUsed);

    [HttpPatch("{id}/consume")]
    public async Task<IActionResult> ConsumeFromInventory(Guid id, [FromBody] ConsumeFilamentRequest request)
    {
        if (request.GramsUsed <= 0)
            return BadRequest(new { message = "GramsUsed must be greater than 0." });

        // Admin can consume even if filament is inactive (inventory operations), but it must exist.
        var filamentExists = await _db.Filaments
            .IgnoreQueryFilters()
            .AnyAsync(x => x.Id == id);

        if (!filamentExists)
            return NotFound(new { message = "Filament not found." });

        const int toleranceGrams = 10;
        var gramsUsed = request.GramsUsed;

        // Pick one spool that can satisfy the request.
        // Policy:
        // 1) Only spools that still have material (RemainingGrams > 0)
        // 2) Must satisfy RemainingGrams + tolerance >= gramsUsed
        // 3) Prefer Opened, then New
        // 4) Oldest first
        var spool = await _db.FilamentSpools
            .Where(x => x.FilamentId == id)
            .Where(x => x.RemainingGrams > 0)
            .Where(x => x.RemainingGrams + toleranceGrams >= gramsUsed)
            .OrderBy(x => x.Status == "Opened" ? 0 : 1)
            .ThenBy(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync();

        if (spool == null)
            return Conflict(new { message = "Not enough inventory to fulfill this request." });

        // Extra safety check (helps in case something changed between query and update)
        if (!SpoolConsumption.CanConsume(spool, gramsUsed, toleranceGrams))
            return Conflict(new { message = "Not enough inventory to fulfill this request." });

        SpoolConsumption.Apply(spool, gramsUsed);
        await _db.SaveChangesAsync();

        return NoContent();
    }
}