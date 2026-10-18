using CampusHub.Domain;
using CampusHub.Reporting;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace CampusHub.Api.Controllers;

[ApiController, Route("api/reports")]
public sealed class ReportsController(LegacyReportsService reports) : ControllerBase
{
    [HttpGet("legacy")]
    public Task<LegacyReportDto> Legacy(string? group = null, [FromQuery, Range(3, 10000)] int limit = 1000,
        CancellationToken ct = default) => reports.GenerateAsync(group, limit, ct);
    [HttpGet("course-stats")]
    public Task<List<CourseStatsDto>> CourseStats(CancellationToken ct) => reports.CourseCountsAsync(ct);
}
