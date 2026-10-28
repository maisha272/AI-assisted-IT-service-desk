using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using ServiceDesk.Api.Data;
using ServiceDesk.Api.Domain;
using ServiceDesk.Api.Services;
using ServiceDesk.Api.Services.Auth;

namespace ServiceDesk.Api.Controllers;

public record LoginRequest([Required, EmailAddress] string Email, [Required] string Password);

public record UserDto(int Id, string Email, string DisplayName, UserRole Role, int? ClientId, string? ClientName);

public record LoginResponse(string Token, DateTime ExpiresAt, UserDto User);

[ApiController]
[Route("api/auth")]
public class AuthController(AppDbContext db, IPasswordHasher<User> hasher, TokenService tokens, ICurrentUser currentUser)
    : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(AuthSetup.LoginRateLimitPolicy)]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.Include(u => u.Client).FirstOrDefaultAsync(u => u.Email == email, ct);

        // Same response for "no such user" and "wrong password", so attackers can't discover valid emails.
        // Users auto-created from inbound email have no password and can't sign in.
        if (user is null || string.IsNullOrEmpty(user.PasswordHash)
            || hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password) == PasswordVerificationResult.Failed)
            return Problem("Invalid email or password.", statusCode: StatusCodes.Status401Unauthorized);

        var (token, expiresAt) = tokens.CreateToken(user);
        return new LoginResponse(token, expiresAt, ToDto(user));
    }

    [HttpGet("me")]
    public async Task<UserDto> Me(CancellationToken ct) => ToDto(await currentUser.GetAsync(ct));

    private static UserDto ToDto(User u) => new(u.Id, u.Email, u.DisplayName, u.Role, u.ClientId, u.Client?.Name);
}
