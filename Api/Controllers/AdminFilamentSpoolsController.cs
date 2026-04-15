using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrintIt.Api.Auth;
using PrintIt.Domain.Entities;
using PrintIt.Infrastructure.Persistence;

namespace PrintIt.Api.Controllers;

[ApiController]
[Route("api/v1/admin/filament-spools")]
[Authorize(Policy = "AdminOnly")]
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
    if (!AdminStoreContext.TryGetStoreId(User, out var storeId))
        return Forbid();

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
        .FirstOrDefaultAsync(x => x.StoreId == storeId && x.Id == request.FilamentId);

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

public record ConsumeSpoolRequest(int GramsUsed);

[HttpPatch("{id}/consume")]
public async Task<IActionResult> Consume(Guid id, [FromBody] ConsumeSpoolRequest request)
{
    if (!AdminStoreContext.TryGetStoreId(User, out var storeId))
        return Forbid();

    var gramsUsed = request.GramsUsed;

    if (gramsUsed <= 0)
        return BadRequest(new { message = "GramsUsed must be greater than 0." });

    var spool = await _db.FilamentSpools
        .Include(x => x.Filament)
        .FirstOrDefaultAsync(x => x.Id == id && x.Filament.StoreId == storeId);

    if (spool == null)
        return NotFound(new { message = "Filament spool not found." });

    // Tolerance: slicer estimates are not perfect; we keep a small safety margin.
    const int toleranceGrams = 10;

    // We allow consuming up to (RemainingGrams + tolerance) but never let RemainingGrams go below 0.
    if (gramsUsed > spool.RemainingGrams + toleranceGrams)
        return Conflict(new
        {
            message = "Not enough filament remaining in this spool.",
            remainingGrams = spool.RemainingGrams,
            toleranceGrams
        });

    spool.RemainingGrams = Math.Max(0, spool.RemainingGrams - gramsUsed);
    spool.LastUsedAtUtc = DateTime.UtcNow;

    if (spool.RemainingGrams == 0)
        spool.Status = "Empty";
    else if (spool.RemainingGrams < spool.InitialGrams)
        spool.Status = "Opened";
    else
        spool.Status = "New";

    await _db.SaveChangesAsync();
    return NoContent();
}




}



