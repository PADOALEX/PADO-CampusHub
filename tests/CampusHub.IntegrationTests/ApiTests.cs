using System.Net;
using System.Net.Http.Json;
using CampusHub.Domain;
using CampusHub.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace CampusHub.IntegrationTests;

public sealed class ApiTests : IClassFixture<CampusHubFactory>
{
    private readonly HttpClient _http;
    public ApiTests(CampusHubFactory factory) => _http = factory.CreateClient();

    [Fact]
    public async Task Students_CanBeCreatedReadAndFiltered()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var response = await _http.PostAsJsonAsync("/api/students", new StudentWriteRequest
        { FirstName = "Alice", LastName = "Smith", Email = $"{suffix}@example.org", GroupName = "PMA-21", Age = 19, LegacyAverageGrade = 90 });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var student = await response.Content.ReadFromJsonAsync<StudentDto>();
        Assert.NotNull(student);
        Assert.Equal("PMA-21", student.GroupName);
        var get = await _http.GetFromJsonAsync<StudentDto>($"/api/students/{student.Id}");
        Assert.Equal("Alice", get?.FirstName);
        var list = await _http.GetFromJsonAsync<PageDto<StudentDto>>("/api/students?group=pma-21&sort=grade&descending=true");
        Assert.Contains(list!.Items, s => s.Id == student.Id);
        var duplicate = await _http.PostAsJsonAsync("/api/students", new StudentWriteRequest
        { FirstName = "Repeat", LastName = "Smith", Email = $"{suffix.ToUpperInvariant()}@example.org" });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }

    [Fact]
    public async Task Enrollment_DuplicateAndCapacityReturnConflict_CancelAllowsReentry()
    {
        var tag = Guid.NewGuid().ToString("N");
        async Task<StudentDto> CreateStudent(string key)
        {
            var res = await _http.PostAsJsonAsync("/api/students", new StudentWriteRequest
            { FirstName = key, LastName = "Test", Email = $"{key}-{tag}@example.org" });
            res.EnsureSuccessStatusCode();
            return (await res.Content.ReadFromJsonAsync<StudentDto>())!;
        }
        var s1 = await CreateStudent("one"); var s2 = await CreateStudent("two");
        var create = await _http.PostAsJsonAsync("/api/courses", new CourseWriteRequest { Title = "One seat", DurationHours = 10, Capacity = 1 });
        create.EnsureSuccessStatusCode();
        var course = (await create.Content.ReadFromJsonAsync<CourseDto>())!;
        var request = new EnrollmentRequest(s1.Id, course.Id);
        Assert.Equal(HttpStatusCode.Created, (await _http.PostAsJsonAsync("/api/enrollments", request)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await _http.PostAsJsonAsync("/api/enrollments", request)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await _http.PostAsJsonAsync("/api/enrollments", new EnrollmentRequest(s2.Id, course.Id))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await _http.DeleteAsync($"/api/enrollments/{s1.Id}/{course.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await _http.PostAsJsonAsync("/api/enrollments", new EnrollmentRequest(s2.Id, course.Id))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await _http.PutAsJsonAsync($"/api/enrollments/{s2.Id}/{course.Id}/grade", new GradeRequest(98m))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await _http.PutAsJsonAsync($"/api/enrollments/{s2.Id}/{course.Id}/status", new { status = "Completed" })).StatusCode);
        var report = await _http.GetFromJsonAsync<LegacyReportDto>("/api/reports/legacy?limit=1000");
        Assert.Contains(report!.Top3, s => s.Id == s2.Id && s.AverageGrade == 98m);
    }
}

public sealed class CampusHubFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    public CampusHubFactory() => _connection.Open();
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=unused;Database=unused;Username=unused;Password=unused",
            ["Database:MigrateOnStartup"] = "false",
            ["SeedData:Enabled"] = "false"
        }));
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<Microsoft.EntityFrameworkCore.Infrastructure.IDbContextOptionsConfiguration<AppDbContext>>();
            services.AddDbContext<AppDbContext>(o => o.UseSqlite(_connection));
            using var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();
            scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();
        });
    }
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _connection.Dispose();
    }
}
