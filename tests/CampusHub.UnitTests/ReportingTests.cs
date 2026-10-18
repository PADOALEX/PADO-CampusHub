using CampusHub.Domain;
using CampusHub.Infrastructure;
using CampusHub.Reporting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CampusHub.UnitTests;

public sealed class ReportingTests
{
    [Fact]
    public async Task LegacyReport_PreservesTop3AndGroupAverage()
    {
        await using var conn = new SqliteConnection("Data Source=:memory:"); await conn.OpenAsync();
        var opts = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options;
        await using var db = new AppDbContext(opts); await db.Database.EnsureCreatedAsync();
        db.Students.AddRange(
            new Student { FirstName = "Ana", LastName = "A", Email = "ana@example.org", NormalizedEmail = "ANA@EXAMPLE.ORG", GroupName = "PMA-21", Age = 19, LegacyAverageGrade = 88.5m },
            new Student { FirstName = "Ben", LastName = "B", Email = "ben@example.org", NormalizedEmail = "BEN@EXAMPLE.ORG", GroupName = "PMA-21", Age = 17, LegacyAverageGrade = 75m },
            new Student { FirstName = "Cat", LastName = "C", Email = "cat@example.org", NormalizedEmail = "CAT@EXAMPLE.ORG", GroupName = "CE-23", Age = 20, LegacyAverageGrade = 95m });
        await db.SaveChangesAsync();
        var report = await new LegacyReportsService(db).GenerateAsync("pma-21", 100, default);
        Assert.Equal(2, report.SelectedGroupStudents.Count);
        Assert.Equal("Cat C", report.BestStudent?.FullName);
        Assert.Equal(3, report.Top3.Count);
        Assert.Equal(86.17m, Math.Round(report.OverallAverage!.Value, 2));
        Assert.True(report.HasUnderageStudents);
        Assert.Equal(81.75m, report.GroupStatistics.Single(x => x.GroupName == "PMA-21").AverageGrade);
        Assert.Equal("CE-23", report.BestGroup?.GroupName);
    }

    [Fact]
    public async Task CompletedCourseGrade_ReplacesLegacySnapshot_AndCourseCountIsOnlyActive()
    {
        await using var conn = new SqliteConnection("Data Source=:memory:"); await conn.OpenAsync();
        var opts = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options;
        await using var db = new AppDbContext(opts); await db.Database.EnsureCreatedAsync();
        var student = new Student { FirstName = "Ada", LastName = "L", Email = "ada@test.org", NormalizedEmail = "ADA@TEST.ORG", LegacyAverageGrade = 55m };
        var course = new Course { Title = "Programming", DurationHours = 20, Capacity = 1 };
        db.Enrollments.Add(new Enrollment { Student = student, Course = course, Grade = 90m, Status = EnrollmentStatus.Completed });
        await db.SaveChangesAsync();
        var service = new LegacyReportsService(db);
        var report = await service.GenerateAsync(null, 100, default);
        Assert.Equal(90m, report.BestStudent?.AverageGrade);
        Assert.Equal(0, (await service.CourseCountsAsync(default)).Single().ActiveStudents);
    }
}
