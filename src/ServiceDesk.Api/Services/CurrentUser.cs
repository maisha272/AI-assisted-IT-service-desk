using Microsoft.EntityFrameworkCore;
using ServiceDesk.Api.Data;
using ServiceDesk.Api.Domain;
using ServiceDesk.Api.Services.Auth;

namespace ServiceDesk.Api.Services;

/// <summary>Who is making the current request. Controllers depend on this, not on HTTP details.</summary>
public interface ICurrentUser
{
    Task<User> GetAsync(CancellationToken ct = default);
}

/// <summary>
/// Resolves the signed-in user from the JWT "sub" claim. The user is re-read from the database (once
/// per request) so role or client changes take effect without waiting for the token to expire.
/// </summary>
public class ClaimsCurrentUser(IHttpContextAccessor http, AppDbContext db) : ICurrentUser
{
    private User? _user;

    public async Task<User> GetAsync(CancellationToken ct = default)
    {
        if (_user is not null) return _user;

        var id = http.HttpContext?.User.GetUserId()
            ?? throw new UnauthorizedAccessException("No authenticated user on this request.");

        _user = await db.Users.Include(u => u.Client).FirstOrDefaultAsync(u => u.Id == id, ct)
            ?? throw new UnauthorizedAccessException("The signed-in user no longer exists.");
        return _user;
    }
}
