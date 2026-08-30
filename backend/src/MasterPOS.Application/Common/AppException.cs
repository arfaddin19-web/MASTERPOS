namespace MasterPOS.Application.Common;

/// <summary>
/// Thrown when a request is well-formed but rejected by a business rule
/// (wrong password, setup already completed, duplicate username, ...).
/// The Api layer catches this and maps it to a client-facing 4xx response
/// instead of letting it surface as an unhandled 500.
/// </summary>
public class AppException : Exception
{
    public AppException(string message) : base(message) { }
}
