namespace Restaurant.Application.Common.Interfaces;

public interface IJwtTokenService
{
    string CreateAccessToken(Guid userId, Guid tenantId, string email, IReadOnlyList<string> roles);
}
