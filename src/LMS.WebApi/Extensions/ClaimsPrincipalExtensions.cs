using System.Security.Claims;
using LMS.Business.Exceptions;

namespace LMS.WebApi.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static int GetUserId(this ClaimsPrincipal principal)
    {
        var raw = principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue("sub");

        if (!int.TryParse(raw, out var id))
        {
            throw new UnauthorizedException("Invalid authentication token.");
        }

        return id;
    }

    public static string GetUserRole(this ClaimsPrincipal principal)
    {
        return principal.FindFirstValue(ClaimTypes.Role)
            ?? throw new UnauthorizedException("User role claim is missing.");
    }
}
