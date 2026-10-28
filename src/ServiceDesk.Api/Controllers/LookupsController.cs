using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServiceDesk.Api.Data;
using ServiceDesk.Api.Domain;
using ServiceDesk.Api.Services.Auth;

namespace ServiceDesk.Api.Controllers;

public record ClientDto(int Id, string Name);
public record AgentDto(int Id, string DisplayName);

/// <summary>Small reference lists the UI needs for dropdowns.</summary>
[ApiController]
[Route("api")]
[Authorize(Roles = Roles.Agent)]
public class LookupsController(AppDbContext db) : ControllerBase
{
    [HttpGet("clients")]
    public async Task<IEnumerable<ClientDto>> Clients(CancellationToken ct) =>
        await db.Clients.OrderBy(c => c.Name).Select(c => new ClientDto(c.Id, c.Name)).ToListAsync(ct);

    [HttpGet("agents")]
    public async Task<IEnumerable<AgentDto>> Agents(CancellationToken ct) =>
        await db.Users.Where(u => u.Role == UserRole.Agent).OrderBy(u => u.DisplayName)
            .Select(u => new AgentDto(u.Id, u.DisplayName)).ToListAsync(ct);
}
