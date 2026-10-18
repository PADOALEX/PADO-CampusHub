using CampusHub.Domain;
using CampusHub.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CampusHub.Reporting;

public sealed class LegacyReportsService(AppDbContext db)
{
    // Member-initialized query shape: EF translates filtering, ordering and aggregates before DTO creation.
    private sealed class ScoreRow
    {
        public int Id { get; init; }
        public string FullName { get; init; } = "";
        public string? GroupName { get; init; }
        public int? Age { get; init; }
        public decimal? AverageGrade { get; init; }
    }

    private IQueryable<ScoreRow> Scores() => db.Students.AsNoTracking()
        .Select(s => new ScoreRow
        {
            Id = s.Id,
            FullName = s.FirstName + " " + s.LastName,
            GroupName = s.GroupName,
            Age = s.Age,
            AverageGrade = s.Enrollments
                .Where(e => e.Status == EnrollmentStatus.Completed && e.Grade != null)
                .Average(e => e.Grade) ?? s.LegacyAverageGrade
        });

    private static IQueryable<ScoreDto> AsDtos(IQueryable<ScoreRow> query) =>
        query.Select(s => new ScoreDto(s.Id, s.FullName, s.GroupName, s.Age, s.AverageGrade));

    public async Task<List<CourseStatsDto>> CourseCountsAsync(CancellationToken ct) => await db.Courses.AsNoTracking()
        .OrderBy(c => c.Id)
        .Select(c => new CourseStatsDto(c.Id, c.Title, c.Enrollments.Count(e => e.Status == EnrollmentStatus.Active), c.Capacity))
        .ToListAsync(ct);

    public async Task<LegacyReportDto> GenerateAsync(string? selectedGroup, int limit, CancellationToken ct)
    {
        var scores = Scores();
        var group = selectedGroup?.Trim().ToLowerInvariant() ?? "";
        var selected = await AsDtos(scores.Where(s => s.GroupName != null && s.GroupName.ToLower() == group).OrderBy(s => s.Id).Take(limit)).ToListAsync(ct);
        var above85 = await AsDtos(scores.Where(s => s.AverageGrade > 85).OrderByDescending(s => s.AverageGrade).ThenBy(s => s.Id).Take(limit)).ToListAsync(ct);
        var sorted = await AsDtos(scores.OrderByDescending(s => s.AverageGrade).ThenBy(s => s.Id).Take(limit)).ToListAsync(ct);
        var names = await db.Students.AsNoTracking().OrderBy(s => s.Id).Select(s => s.FirstName + " " + s.LastName).Take(limit).ToListAsync(ct);
        var grouped = scores.GroupBy(s => s.GroupName)
            .Select(g => new { GroupName = g.Key ?? "(unassigned)", StudentCount = g.Count(), AverageGrade = g.Average(s => s.AverageGrade) });
        var stats = await grouped.OrderBy(g => g.GroupName).Take(limit)
            .Select(g => new GroupStatsDto(g.GroupName, g.StudentCount, g.AverageGrade)).ToListAsync(ct);
        var best = await AsDtos(scores.Where(s => s.AverageGrade != null).OrderByDescending(s => s.AverageGrade).ThenBy(s => s.Id)).FirstOrDefaultAsync(ct);
        var underage = await AsDtos(scores.Where(s => s.Age < 18).OrderBy(s => s.Id).Take(limit)).ToListAsync(ct);
        var groups = await db.Students.AsNoTracking().Where(s => s.GroupName != null).Select(s => s.GroupName!).Distinct().OrderBy(s => s).Take(limit).ToListAsync(ct);
        var top3 = await AsDtos(scores.Where(s => s.AverageGrade != null).OrderByDescending(s => s.AverageGrade).ThenBy(s => s.Id).Take(3)).ToListAsync(ct);
        var average = await scores.AverageAsync(s => s.AverageGrade, ct);
        var bestGroup = await grouped.Where(g => g.AverageGrade != null).OrderByDescending(g => g.AverageGrade).ThenBy(g => g.GroupName)
            .Select(g => new GroupStatsDto(g.GroupName, g.StudentCount, g.AverageGrade)).FirstOrDefaultAsync(ct);
        var truncated = await db.Students.CountAsync(ct) > limit ||
            await db.Students.AsNoTracking().Where(s => s.GroupName != null).Select(s => s.GroupName!).Distinct().CountAsync(ct) > limit;
        return new LegacyReportDto(selected, above85, sorted, names, stats, best, underage.Count > 0, underage,
            groups, top3, average, stats, bestGroup, truncated, limit);
    }
}
