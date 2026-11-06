using CampusHub.Api.Services;
using CampusHub.Domain;
using CampusHub.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace CampusHub.IntegrationTests;

public sealed class PostgreSqlCapacityTests
{
    [Fact]
    public async Task ConcurrentEnrollmentsCannotExceedOneAvailableSeat()
    {
        var connection = Environment.GetEnvironmentVariable("CAMPUSHUB_TEST_CONNECTION")
            ?? throw new InvalidOperationException("Set CAMPUSHUB_TEST_CONNECTION to a disposable PostgreSQL instance with CREATE DATABASE permission.");
        var database = "campushub_test_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(connection);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{database}\"", admin))
            await create.ExecuteNonQueryAsync();
        var isolated = new NpgsqlConnectionStringBuilder(connection) { Database = database }.ToString();
        AppDbContext NewContext() => new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(isolated).Options);
        try
        {
            int courseId;
            int[] students;
            await using (var setup = NewContext())
            {
                await setup.Database.MigrateAsync();
                setup.Students.AddRange(
                    new Student { FirstName = "One", Email = "one@test.invalid", NormalizedEmail = "ONE@TEST.INVALID" },
                    new Student { FirstName = "Two", Email = "two@test.invalid", NormalizedEmail = "TWO@TEST.INVALID" });
                var course = new Course { Title = "Concurrent enrollment", Capacity = 1, DurationHours = 20 };
                setup.Courses.Add(course);
                await setup.SaveChangesAsync();
                courseId = course.Id;
                students = await setup.Students.Select(student => student.Id).ToArrayAsync();
            }
            async Task<bool> Enroll(int studentId)
            {
                await using var db = NewContext();
                try
                {
                    await new EnrollmentService(db, new CourseService(db)).EnrollAsync(new(studentId, courseId), default);
                    return true;
                }
                catch (DomainProblem problem) when (problem.StatusCode == 409) { return false; }
            }
            var outcomes = await Task.WhenAll(students.Select(Enroll)).WaitAsync(TimeSpan.FromSeconds(20));
            Assert.Single(outcomes, success => success);
            await using var verify = NewContext();
            Assert.Equal(1, await verify.Enrollments.CountAsync(enrollment => enrollment.CourseId == courseId && enrollment.Status == EnrollmentStatus.Active));
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP DATABASE \"{database}\" WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }
}
