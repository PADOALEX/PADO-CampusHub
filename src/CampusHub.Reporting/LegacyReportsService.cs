using CampusHub.Domain;
using CampusHub.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CampusHub.Reporting;

public sealed class LegacyReportsService(AppDbContext db)
{
    // A graded completed enrollment takes priority over the imported historical average.
    // This expression stays IQueryable and is translated by EF Core (no implicit AsEnumerable).
    private IQueryable<ScoreDto> Scores() => db.Students.AsNoTracking()
        .Select(s => new ScoreDto(s.Id, s.FirstName + " " + s.LastName, s.GroupName, s.Age,
            s.Enrollments.Where(e => e.Status == EnrollmentStatus.Completed && e.Grade != null).Average(e => e.Grade) ?? s.LegacyAverageGrade));

    public async Task<List<CourseStatsDto>> CourseCountsAsync(CancellationToken ct) => await db.Courses.AsNoTracking()
        .OrderBy(c => c.Id)
        .Select(c => new CourseStatsDto(c.Id, c.Title, c.Enrollments.Count(e => e.Status == EnrollmentStatus.Active), c.Capacity))
        .ToListAsync(ct);

    public async Task<LegacyReportDto> GenerateAsync(string? selectedGroup, int limit, CancellationToken ct)
    {
        var scores = Scores();
        var group = selectedGroup?.Trim().ToLowerInvariant() ?? "";
        var selected = await scores.Where(s => s.GroupName != null && s.GroupName.ToLower() == group).OrderBy(s => s.Id).Take(limit).ToListAsync(ct);
        var above85 = await scores.Where(s => s.AverageGrade > 85).OrderByDescending(s => s.AverageGrade).ThenBy(s => s.Id).Take(limit).ToListAsync(ct);
        var sorted = await scores.OrderByDescending(s => s.AverageGrade).ThenBy(s => s.Id).Take(limit).ToListAsync(ct);
        var names = await db.Students.AsNoTracking().OrderBy(s => s.Id).Select(s => s.FirstName + " " + s.LastName).Take(limit).ToListAsync(ct);
        var stats = await scores.GroupBy(s => s.GroupName)
            .Select(g => new GroupStatsDto(g.Key ?? "(unassigned)", g.Count(), g.Average(s => s.AverageGrade)))
            .OrderBy(g => g.GroupName).Take(limit).ToListAsync(ct);
        var best = await scores.Where(s => s.AverageGrade != null).OrderByDescending(s => s.AverageGrade).ThenBy(s => s.Id).FirstOrDefaultAsync(ct);
        var underage = await scores.Where(s => s.Age < 18).OrderBy(s => s.Id).Take(limit).ToListAsync(ct);
        var groups = await db.Students.AsNoTracking().Where(s => s.GroupName != null).Select(s => s.GroupName!).Distinct().OrderBy(s => s).Take(limit).ToListAsync(ct);
        var top3 = await scores.Where(s => s.AverageGrade != null).OrderByDescending(s => s.AverageGrade).ThenBy(s => s.Id).Take(3).ToListAsync(ct);
        var average = await scores.AverageAsync(s => s.AverageGrade, ct);
        var bestGroup = await scores.GroupBy(s => s.GroupName)
            .Select(g => new GroupStatsDto(g.Key ?? "(unassigned)", g.Count(), g.Average(s => s.AverageGrade)))
            .Where(g => g.AverageGrade != null).OrderByDescending(g => g.AverageGrade).ThenBy(g => g.GroupName).FirstOrDefaultAsync(ct);
        // Each list is capped by the caller's explicit limit, never by loading all students.
        var truncated = await db.Students.CountAsync(ct) > limit ||
            await db.Students.AsNoTracking().Where(s => s.GroupName != null).Select(s => s.GroupName!).Distinct().CountAsync(ct) > limit;
        return new LegacyReportDto(selected, above85, sorted, names, stats, best, underage.Count > 0, underage,
            groups, top3, average, stats, bestGroup, truncated, limit);
    }
}
