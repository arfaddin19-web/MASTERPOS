using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using MasterPOS.Application.Auth;
using MasterPOS.Domain.Auth;
using Microsoft.IdentityModel.Tokens;

namespace MasterPOS.Api.Auth;

/// <summary>
/// The Api-layer implementation of Application's <see cref="ITokenService"/>
/// — this is the only place the JWT signing key / issuer / audience
/// configuration is read.
/// </summary>
public class JwtTokenService : ITokenService
{
    private readonly IConfiguration _config;

    public JwtTokenService(IConfiguration config) => _config = config;

    public (string Token, DateTime ExpiresAtUtc) CreateAccessToken(User user, string roleName)
    {
        var jwt = _config.GetSection("Jwt");
        var signingKey = jwt["SigningKey"]
            ?? throw new InvalidOperationException("Jwt:SigningKey is not configured.");
        var expiryMinutes = int.TryParse(jwt["ExpiryMinutes"], out var m) ? m : 480;

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Role, roleName),
            new("companyId", user.CompanyId.ToString()),
        };
        if (user.DefaultBranchId is { } branchId)
            claims.Add(new Claim("branchId", branchId.ToString()));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiresAtUtc = DateTime.UtcNow.AddMinutes(expiryMinutes);

        var token = new JwtSecurityToken(
            issuer: jwt["Issuer"],
            audience: jwt["Audience"],
            claims: claims,
            expires: expiresAtUtc,
            signingCredentials: creds);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAtUtc);
    }
}
