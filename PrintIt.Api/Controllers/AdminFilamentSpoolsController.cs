using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrintIt.Domain.Entities;
using PrintIt.Infrastructure.Persistence;

namespace PrintIt.Api.Controllers;

[ApiController]
[Route("api/v1/admin/filament-spools")]
public class AdminFilamentSpoolsController : ControllerBase
{
    private readonly AppDbContext _db;

    public AdminFilamentSpoolsController(AppDbContext db)
    {
        _db = db;
    }

    public record CreateFilamentSpoolRequest(
        Guid FilamentId,
        int InitialGrams,
        int? RemainingGrams
    );

[HttpPost]
public async Task<IActionResult> Create([FromBody] CreateFilamentSpoolRequest request)
{
    var initialGrams = request.InitialGrams;

    if (initialGrams <= 0)
        return BadRequest(new { message = "InitialGrams must be greater than 0." });

    var remainingGrams = request.RemainingGrams ?? initialGrams;

    if (remainingGrams <= 0)
        return BadRequest(new { message = "RemainingGrams must be greater than 0." });

    if (remainingGrams > initialGrams)
        return BadRequest(new { message = "RemainingGrams cannot be greater than InitialGrams." });

    var filament = await _db.Filaments
        .IgnoreQueryFilters()
        .FirstOrDefaultAsync(x => x.Id == request.FilamentId);

    if (filament == null)
        return NotFound(new { message = "Filament not found." });

    if (!filament.IsActive)
        return Conflict(new { message = "Filament is inactive." });

    var entity = new FilamentSpool
    {
        FilamentId = request.FilamentId,
        InitialGrams = initialGrams,
        RemainingGrams = remainingGrams,
        Status = remainingGrams == initialGrams ? "New" : "Opened"
    };

    _db.FilamentSpools.Add(entity);
    await _db.SaveChangesAsync();

    return Created($"/api/v1/admin/filament-spools/{entity.Id}", new
    {
        entity.Id,
        entity.FilamentId,
        entity.InitialGrams,
        entity.RemainingGrams,
        entity.Status,
        entity.CreatedAtUtc,
        entity.LastUsedAtUtc
    });

}
}

