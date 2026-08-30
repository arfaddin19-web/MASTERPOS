namespace MasterPOS.Application.Common;

/// <summary>
/// Resolves the authenticated caller's identity for the current request —
/// the <c>companyId</c>/<c>sub</c> claims <see cref="Auth.ITokenService"/> puts on
/// the JWT at login. Implemented in the Api layer (the only place that
/// knows about HttpContext); every module below Api depends on this
/// instead of re-deriving it from claims itself. Single company per
/// install (see Company's class remarks), so CompanyId is always the
/// caller's own company — there's no cross-tenant scoping to get wrong.
/// </summary>
public interface ICurrentUserContext
{
    Guid CompanyId { get; }
    Guid UserId { get; }

    /// <summary>The caller's default branch, from the optional <c>branchId</c> JWT claim —
    /// null for a user with no default branch set. Modules that need a branch (Sales, at
    /// minimum) should throw a clear <see cref="AppException"/> rather than let this npe.</summary>
    Guid? BranchId { get; }
}
