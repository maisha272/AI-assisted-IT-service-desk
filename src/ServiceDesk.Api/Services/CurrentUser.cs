using Microsoft.EntityFrameworkCore;
using ServiceDesk.Api.Data;
using ServiceDesk.Api.Domain;

namespace ServiceDesk.Api.Services;

/// <summary>Who is making the current request. Controllers depend on this, not on HTTP details.</summary>
public interface ICurrentUser
{
    Task<User> GetAsync(CancellationToken ct = default);
}

/// <summary>
/// Temporary implementation for the first vertical slice: everyone is the first seeded agent.
/// Replaced by a JWT-claims-based implementation when authentication is added.
/// </summary>
public class FirstAgentCurrentUser(AppDbContext db) : ICurrentUser
{
    public Task<User> GetAsync(CancellationToken ct = default) =>
        db.Users.OrderBy(u => u.Id).FirstAsync(u => u.Role == UserRole.Agent, ct);
}
