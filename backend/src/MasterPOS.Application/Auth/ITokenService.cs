using MasterPOS.Domain.Auth;

namespace MasterPOS.Application.Auth;

/// <summary>
/// Issues the signed access token for an authenticated user. Implemented in
/// the Api layer, where the signing key / issuer / audience configuration
/// lives — Application stays unaware of JWT specifics.
/// </summary>
public interface ITokenService
{
    (string Token, DateTime ExpiresAtUtc) CreateAccessToken(User user, string roleName);
}
