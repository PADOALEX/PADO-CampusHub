using CampusHub.Domain;
using CampusHub.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CampusHub.Api.Services;

public sealed class StudentService(AppDbContext db)
{
    public async Task<PageDto<StudentDto>> SearchAsync(int page, int size, string? search, string? group, int? olderThan, string? sort, bool descending, CancellationToken ct)
    {
        IQueryable<Student> q = db.Students.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            q = q.Where(s => (s.FirstName + " " + s.LastName).ToLower().Contains(term) || s.Email.ToLower().Contains(term));
        }
        if (!string.IsNullOrWhiteSpace(group))
        {
            var normalized = group.Trim().ToLowerInvariant();
            q = q.Where(s => s.GroupName != null && s.GroupName.ToLower() == normalized);
        }
        if (olderThan.HasValue) q = q.Where(s => s.Age.HasValue && s.Age > olderThan.Value);
        var total = await q.CountAsync(ct);
        var projected = q.Select(s => new StudentDto(s.Id, s.FirstName, s.LastName, s.Email, s.GroupName, s.Age,
            s.Enrollments.Where(e => e.Status == EnrollmentStatus.Completed && e.Grade != null).Average(e => e.Grade) ?? s.LegacyAverageGrade));
        var ordered = (sort?.ToLowerInvariant(), descending) switch
        {
            ("age", true) => projected.OrderByDescending(s => s.Age).ThenBy(s => s.Id),
            ("age", false) => projected.OrderBy(s => s.Age).ThenBy(s => s.Id),
            ("grade", true) => projected.OrderByDescending(s => s.AverageGrade).ThenBy(s => s.Id),
            ("grade", false) => projected.OrderBy(s => s.AverageGrade).ThenBy(s => s.Id),
            ("name", true) => projected.OrderByDescending(s => s.LastName).ThenByDescending(s => s.FirstName).ThenBy(s => s.Id),
            ("name", false) => projected.OrderBy(s => s.LastName).ThenBy(s => s.FirstName).ThenBy(s => s.Id),
            (_, true) => projected.OrderByDescending(s => s.Id),
            _ => projected.OrderBy(s => s.Id)
        };
        return new PageDto<StudentDto>(await ordered.Skip((page - 1) * size).Take(size).ToListAsync(ct), page, size, total);
    }

    public Task<StudentDto?> GetAsync(int id, CancellationToken ct) => db.Students.AsNoTracking()
        .Where(s => s.Id == id)
        .Select(s => new StudentDto(s.Id, s.FirstName, s.LastName, s.Email, s.GroupName, s.Age,
            s.Enrollments.Where(e => e.Status == EnrollmentStatus.Completed && e.Grade != null).Average(e => e.Grade) ?? s.LegacyAverageGrade))
        .FirstOrDefaultAsync(ct);

    public async Task<StudentDto> CreateAsync(StudentWriteRequest request, CancellationToken ct)
    {
        var email = request.Email.Trim();
        var normalized = email.ToUpperInvariant();
        if (await db.Students.AnyAsync(s => s.NormalizedEmail == normalized, ct)) throw new DomainProblem(409, "Student email already exists.");
        var student = new Student();
        Apply(student, request);
        db.Students.Add(student);
        await db.SaveChangesAsync(ct);
        return (await GetAsync(student.Id, ct))!;
    }
    public async Task<bool> UpdateAsync(int id, StudentWriteRequest request, CancellationToken ct)
    {
        var student = await db.Students.FindAsync(new object[] { id }, ct);
        if (student is null) return false;
        var normalized = request.Email.Trim().ToUpperInvariant();
        if (await db.Students.AnyAsync(s => s.NormalizedEmail == normalized && s.Id != id, ct))
            throw new DomainProblem(409, "Student email already exists.");
        Apply(student, request);
        await db.SaveChangesAsync(ct);
        return true;
    }
    public async Task<bool> DeleteAsync(int id, CancellationToken ct)
    {
        var student = await db.Students.FindAsync(new object[] { id }, ct);
        if (student is null) return false;
        db.Students.Remove(student); await db.SaveChangesAsync(ct); return true;
    }
    private static void Apply(Student student, StudentWriteRequest r)
    {
        student.FirstName = r.FirstName.Trim();
        student.LastName = r.LastName.Trim();
        student.Email = r.Email.Trim();
        student.NormalizedEmail = student.Email.ToUpperInvariant();
        student.GroupName = string.IsNullOrWhiteSpace(r.GroupName) ? null : r.GroupName.Trim();
        student.Age = r.Age;
        student.LegacyAverageGrade = r.LegacyAverageGrade;
    }
}
