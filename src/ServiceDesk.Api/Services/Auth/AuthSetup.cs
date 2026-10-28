using System.IdentityModel.Tokens.Jwt;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using ServiceDesk.Api.Domain;

namespace ServiceDesk.Api.Services.Auth;

public static class Roles
{
    public const string Agent = nameof(UserRole.Agent);
    public const string ClientStaff = nameof(UserRole.ClientStaff);
}

public static class AuthSetup
{
    public const string LoginRateLimitPolicy = "login";

    public static IServiceCollection AddServiceDeskAuth(this IServiceCollection services, IConfiguration config, IHostEnvironment env)
    {
        var jwt = config.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
        if (string.IsNullOrWhiteSpace(jwt.SigningKey))
        {
            if (env.IsProduction())
                throw new InvalidOperationException("Jwt:SigningKey must be configured in production (set the Jwt__SigningKey environment variable).");
            jwt.SigningKey = JwtOptions.GenerateRandomKey();
        }
        if (jwt.SigningKey.Length < 32)
            throw new InvalidOperationException("Jwt:SigningKey must be at least 32 characters.");

        services.Configure<JwtOptions>(o =>
        {
            o.SigningKey = jwt.SigningKey;
            o.ExpiryMinutes = jwt.ExpiryMinutes;
        });
        services.AddSingleton<TokenService>();
        services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, ClaimsCurrentUser>();

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                // Keep claim names exactly as issued ("sub", "role") instead of mapping them to long
                // WS-Federation URIs.
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = JwtOptions.Issuer,
                    ValidAudience = JwtOptions.Audience,
                    IssuerSigningKey = jwt.GetKey(),
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(1),
                    NameClaimType = TokenService.NameClaim,
                    RoleClaimType = TokenService.RoleClaim,
                };
            });

        services.AddAuthorization(options =>
        {
            // Secure by default: every endpoint requires a signed-in user unless marked [AllowAnonymous].
            options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
        });

        // Slow down password guessing: at most 10 login attempts per minute per IP address.
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(LoginRateLimitPolicy, http => RateLimitPartition.GetFixedWindowLimiter(
                http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
        });

        return services;
    }

    public static int? GetUserId(this System.Security.Claims.ClaimsPrincipal principal) =>
        int.TryParse(principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var id) ? id : null;
}
