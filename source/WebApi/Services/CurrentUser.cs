using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

using Project.Application.Common.Interfaces;

namespace Project.WebApi.Services;

public class CurrentUser(IHttpContextAccessor httpContextAccessor) : IUser
{
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;

    public Guid? Id => Guid.TryParse(GetClaimValue(JwtRegisteredClaimNames.Jti), out var id) ? id : null;

    public string? Username => GetClaimValue(ClaimTypes.Name)
                               ?? GetClaimValue(ClaimTypes.NameIdentifier)
                               ?? GetClaimValue(JwtRegisteredClaimNames.Sub)
                               ?? GetClaimValue("sub");

    public string? Role => GetClaimValue(ClaimTypes.Role);
    public string? Email => GetClaimValue(ClaimTypes.Email);

    private string? GetClaimValue(string claimType)
    {
        return _httpContextAccessor.HttpContext?.User?.FindFirstValue(claimType);
    }
}
