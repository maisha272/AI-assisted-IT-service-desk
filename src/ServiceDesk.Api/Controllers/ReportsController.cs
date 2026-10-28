using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServiceDesk.Api.Services;
using ServiceDesk.Api.Services.Auth;

namespace ServiceDesk.Api.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize(Roles = Roles.Agent)]
public class ReportsController(ReportService reports) : ControllerBase
{
    /// <summary>Report for the last <paramref name="days"/> days (7–366, default 90).</summary>
    [HttpGet]
    public Task<ReportDto> Get([FromQuery] int days = 90, CancellationToken ct = default) => reports.GetAsync(days, ct);
}
