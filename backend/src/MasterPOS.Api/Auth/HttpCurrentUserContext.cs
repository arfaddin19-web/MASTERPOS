using System.IdentityModel.Tokens.Jwt;
using MasterPOS.Application.Common;

namespace MasterPOS.Api.Auth;

/// <summary>
/// Reads <see cref="ICurrentUserContext"/> off the authenticated request's
/// JWT claims — the <c>companyId</c> claim and the standard <c>sub</c>
/// <see cref="JwtTokenService"/> puts on the token at login.
/// </summary>
public class HttpCurrentUserContext : ICurrentUserContext
{
    private readonly IHttpContextAccessor _accessor;

    public HttpCurrentUserContext(IHttpContextAccessor accessor) => _accessor = accessor;

    public Guid CompanyId => GetGuidClaim("companyId");

    public Guid UserId => GetGuidClaim(JwtRegisteredClaimNames.Sub);

    public Guid? BranchId
    {
        get
        {
            var value = CurrentUser.FindFirst("branchId")?.Value;
            return value is null ? null : Guid.Parse(value);
        }
    }

    private System.Security.Claims.ClaimsPrincipal CurrentUser
        => _accessor.HttpContext?.User ?? throw new InvalidOperationException("No authenticated request in scope.");

    private Guid GetGuidClaim(string claimType)
    {
        var value = CurrentUser.FindFirst(claimType)?.Value
            ?? throw new InvalidOperationException($"Claim '{claimType}' is missing from the current request.");
        return Guid.Parse(value);
    }
}
