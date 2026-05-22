namespace Arbiter.Api;

public interface IAuthenticator
{
    Task<AuthResult> AuthenticateAsync(string? bearerToken, CancellationToken ct);
}

public record AuthResult(bool IsAuthenticated, Guid? AccountId = null, Guid? ApplicationId = null);
