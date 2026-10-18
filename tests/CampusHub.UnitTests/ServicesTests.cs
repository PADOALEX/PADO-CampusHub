using System.ComponentModel.DataAnnotations;
using CampusHub.Api.Services;
using CampusHub.Domain;
using CampusHub.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CampusHub.UnitTests;

public sealed class ServicesTests
{
    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void GradeDto_RejectsInvalidValues(int grade)
    {
        var dto = new GradeRequest(grade);
        var errors = new List<ValidationResult>();
        Assert.False(Validator.TryValidateObject(dto, new ValidationContext(dto), errors, true));
    }

    [Fact]
    public async Task EnrollmentService_EnforcesCapacityAndCanReactivateCancelled()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using var db = new AppDbContext(options);
        await db.Database.EnsureCreatedAsync();
        db.Students.AddRange(
            new Student { FirstName = "One", Email = "one@test.org", NormalizedEmail = "ONE@TEST.ORG" },
            new Student { FirstName = "Two", Email = "two@test.org", NormalizedEmail = "TWO@TEST.ORG" });
        db.Courses.Add(new Course { Title = "Physics", Capacity = 1, DurationHours = 20 });
        await db.SaveChangesAsync();
        var course = await db.Courses.SingleAsync();
        var students = await db.Students.OrderBy(s => s.Id).ToListAsync();
        var courseService = new CourseService(db);
        var service = new EnrollmentService(db, courseService);
        var first = new EnrollmentRequest(students[0].Id, course.Id);
        var second = new EnrollmentRequest(students[1].Id, course.Id);
        await service.EnrollAsync(first, default);
        Assert.Equal(409, (await Assert.ThrowsAsync<DomainProblem>(() => service.EnrollAsync(first, default))).StatusCode);
        Assert.Equal(409, (await Assert.ThrowsAsync<DomainProblem>(() => service.EnrollAsync(second, default))).StatusCode);
        await service.SetStatusAsync(first.StudentId, first.CourseId, EnrollmentStatus.Cancelled, default);
        await service.EnrollAsync(second, default);
        Assert.Equal(409, (await Assert.ThrowsAsync<DomainProblem>(() => service.EnrollAsync(first, default))).StatusCode);
        Assert.True(await service.SetGradeAsync(second.StudentId, second.CourseId, 88m, default));
        Assert.True(await service.SetStatusAsync(second.StudentId, second.CourseId, EnrollmentStatus.Completed, default));
        await service.EnrollAsync(first, default);
        Assert.Equal(1, await db.Enrollments.CountAsync(e => e.Status == EnrollmentStatus.Active));
    }

    [Fact]
    public async Task StudentService_EnforcesUniqueNormalizedEmail()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using var db = new AppDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var service = new StudentService(db);
        var request = new StudentWriteRequest { FirstName = "A", Email = "Test@Example.org" };
        var first = await service.CreateAsync(request, default);
        Assert.Equal("A", first.FirstName);
        Assert.Equal(409, (await Assert.ThrowsAsync<DomainProblem>(() => service.CreateAsync(
            new StudentWriteRequest { FirstName = "B", Email = "test@example.org" }, default))).StatusCode);
    }
}
