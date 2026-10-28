using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using ServiceDesk.Api.Domain;

namespace ServiceDesk.Api.Services.Auth;

public class JwtOptions
{
    public const string Issuer = "ServiceDesk";
    public const string Audience = "ServiceDesk";

    /// <summary>
    /// HMAC signing key (at least 32 characters). Comes from configuration / environment
    /// (<c>Jwt__SigningKey</c>) — never committed. Outside Production a random key is generated at
    /// startup if none is set, which simply means tokens stop working when the app restarts.
    /// </summary>
    public string SigningKey { get; set; } = "";

    public int ExpiryMinutes { get; set; } = 480;

    public SymmetricSecurityKey GetKey() => new(Encoding.UTF8.GetBytes(SigningKey));

    public static string GenerateRandomKey() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
}

public class TokenService(IOptions<JwtOptions> options, TimeProvider clock)
{
    public const string RoleClaim = "role";
    public const string NameClaim = "name";

    public (string Token, DateTime ExpiresAt) CreateToken(User user)
    {
        var jwt = options.Value;
        var now = clock.GetUtcNow().UtcDateTime;
        var expires = now.AddMinutes(jwt.ExpiryMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(NameClaim, user.DisplayName),
            new(RoleClaim, user.Role.ToString()),
        };

        var token = new JwtSecurityToken(
            issuer: JwtOptions.Issuer,
            audience: JwtOptions.Audience,
            claims: claims,
            notBefore: now,
            expires: expires,
            signingCredentials: new SigningCredentials(jwt.GetKey(), SecurityAlgorithms.HmacSha256));

        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}
