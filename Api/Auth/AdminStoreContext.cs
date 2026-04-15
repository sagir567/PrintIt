using System.Security.Claims;

namespace PrintIt.Api.Auth;

public static class AdminStoreContext
{
    public const string StoreIdClaimType = "store_id";

    public static bool TryGetStoreId(ClaimsPrincipal principal, out Guid storeId)
    {
        var raw = principal.FindFirstValue(StoreIdClaimType);
        return Guid.TryParse(raw, out storeId);
    }
}