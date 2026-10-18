using CampusHub.Domain;
using CampusHub.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CampusHub.Api.Services;

public sealed class CourseService(AppDbContext db)
{
    public async Task<PageDto<CourseDto>> SearchAsync(int page, int size, string? search, int? longerThan, string? sort, bool descending, CancellationToken ct)
    {
        IQueryable<Course> q = db.Courses.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search)) { var term = search.Trim().ToLowerInvariant(); q = q.Where(c => c.Title.ToLower().Contains(term)); }
        if (longerThan.HasValue) q = q.Where(c => c.DurationHours.HasValue && c.DurationHours > longerThan.Value);
        var total = await q.CountAsync(ct);
        var ordered = (sort?.ToLowerInvariant(), descending) switch
        {
            ("duration", true) => q.OrderByDescending(c => c.DurationHours).ThenBy(c => c.Id),
            ("duration", false) => q.OrderBy(c => c.DurationHours).ThenBy(c => c.Id),
            ("title", true) => q.OrderByDescending(c => c.Title).ThenBy(c => c.Id),
            ("title", false) => q.OrderBy(c => c.Title).ThenBy(c => c.Id),
            (_, true) => q.OrderByDescending(c => c.Id),
            _ => q.OrderBy(c => c.Id)
        };
        var items = await ordered.Skip((page - 1) * size).Take(size)
            .Select(c => new CourseDto(c.Id, c.Title, c.Description, c.DurationHours, c.Capacity,
                c.Enrollments.Count(e => e.Status == EnrollmentStatus.Active))).ToListAsync(ct);
        return new PageDto<CourseDto>(items, page, size, total);
    }

    public Task<CourseDto?> GetAsync(int id, CancellationToken ct) => db.Courses.AsNoTracking().Where(c => c.Id == id)
        .Select(c => new CourseDto(c.Id, c.Title, c.Description, c.DurationHours, c.Capacity,
            c.Enrollments.Count(e => e.Status == EnrollmentStatus.Active))).FirstOrDefaultAsync(ct);
    public async Task<CourseDto> CreateAsync(CourseWriteRequest r, CancellationToken ct)
    {
        var course = new Course { Title = r.Title.Trim(), Description = r.Description?.Trim() ?? "", DurationHours = r.DurationHours, Capacity = r.Capacity };
        db.Courses.Add(course); await db.SaveChangesAsync(ct); return (await GetAsync(course.Id, ct))!;
    }
    public async Task<bool> UpdateAsync(int id, CourseWriteRequest r, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var course = await LockCourseAsync(id, ct);
        if (course is null) return false;
        var occupied = await db.Enrollments.CountAsync(e => e.CourseId == id && e.Status == EnrollmentStatus.Active, ct);
        if (r.Capacity < occupied) throw new DomainProblem(409, "Capacity cannot be below active enrollment count.");
        course.Title = r.Title.Trim(); course.Description = r.Description?.Trim() ?? ""; course.DurationHours = r.DurationHours; course.Capacity = r.Capacity;
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return true;
    }
    public async Task<bool> DeleteAsync(int id, CancellationToken ct)
    {
        var course = await db.Courses.FindAsync(new object[] { id }, ct);
        if (course is null) return false;
        db.Courses.Remove(course); await db.SaveChangesAsync(ct); return true;
    }
    // Serialize capacity-changing operations on the same course in PostgreSQL.
    public Task<Course?> LockCourseAsync(int id, CancellationToken ct) =>
        db.Database.IsNpgsql()
            ? db.Courses.FromSqlInterpolated($"SELECT * FROM \"Courses\" WHERE \"Id\" = {id} FOR UPDATE").SingleOrDefaultAsync(ct)
            : db.Courses.SingleOrDefaultAsync(c => c.Id == id, ct);
}
