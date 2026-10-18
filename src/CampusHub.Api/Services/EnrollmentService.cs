using CampusHub.Domain;
using CampusHub.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CampusHub.Api.Services;

public sealed class EnrollmentService(AppDbContext db, CourseService courses)
{
    public async Task<EnrollmentDto> EnrollAsync(EnrollmentRequest r, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var course = await courses.LockCourseAsync(r.CourseId, ct);
        if (course is null) throw new DomainProblem(404, "Course not found.");
        if (!await db.Students.AnyAsync(s => s.Id == r.StudentId, ct)) throw new DomainProblem(404, "Student not found.");
        var existing = await db.Enrollments.FindAsync(new object[] { r.StudentId, r.CourseId }, ct);
        if (existing is not null && existing.Status != EnrollmentStatus.Cancelled) throw new DomainProblem(409, "Student is already enrolled in this course.");
        var taken = await db.Enrollments.CountAsync(e => e.CourseId == r.CourseId && e.Status == EnrollmentStatus.Active, ct);
        if (taken >= course.Capacity) throw new DomainProblem(409, "Course is full.");
        Enrollment enrollment;
        if (existing is null)
        {
            enrollment = new Enrollment { StudentId = r.StudentId, CourseId = r.CourseId };
            db.Enrollments.Add(enrollment);
        }
        else
        {
            existing.Status = EnrollmentStatus.Active; existing.Grade = null; existing.EnrollmentDateUtc = DateTime.UtcNow;
            enrollment = existing;
        }
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return Map(enrollment);
    }
    public async Task<bool> SetStatusAsync(int studentId, int courseId, EnrollmentStatus status, CancellationToken ct)
    {
        if (!Enum.IsDefined(status)) throw new DomainProblem(400, "Unknown status.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var course = await courses.LockCourseAsync(courseId, ct);
        if (course is null) return false;
        var enrollment = await db.Enrollments.FindAsync(new object[] { studentId, courseId }, ct);
        if (enrollment is null) return false;
        if (enrollment.Status == status) { await tx.CommitAsync(ct); return true; }
        if (enrollment.Status == EnrollmentStatus.Completed)
            throw new DomainProblem(409, "Completed enrollment is final.");
        if (enrollment.Status == EnrollmentStatus.Cancelled && status == EnrollmentStatus.Completed)
            throw new DomainProblem(409, "Cancelled enrollment must be reactivated before completion.");
        if (status == EnrollmentStatus.Completed && !enrollment.Grade.HasValue)
            throw new DomainProblem(409, "Grade is required before completing a course.");
        if (status == EnrollmentStatus.Active)
        {
            var taken = await db.Enrollments.CountAsync(e => e.CourseId == courseId && e.Status == EnrollmentStatus.Active, ct);
            if (taken >= course.Capacity) throw new DomainProblem(409, "Course is full.");
            enrollment.Grade = null;
            enrollment.EnrollmentDateUtc = DateTime.UtcNow;
        }
        enrollment.Status = status;
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return true;
    }
    public async Task<bool> SetGradeAsync(int studentId, int courseId, decimal grade, CancellationToken ct)
    {
        if (grade < 0 || grade > 100) throw new DomainProblem(400, "Grade must be between 0 and 100.");
        var e = await db.Enrollments.FindAsync(new object[] { studentId, courseId }, ct);
        if (e is null) return false;
        if (e.Status == EnrollmentStatus.Cancelled) throw new DomainProblem(409, "Cannot grade a cancelled enrollment.");
        e.Grade = grade; await db.SaveChangesAsync(ct); return true;
    }
    public Task<List<EnrollmentDto>> ByStudentAsync(int id, CancellationToken ct) => db.Enrollments.AsNoTracking()
        .Where(e => e.StudentId == id).OrderBy(e => e.CourseId)
        .Select(e => new EnrollmentDto(e.StudentId, e.CourseId, e.Status, e.Grade, e.EnrollmentDateUtc,
            e.Student.FirstName + " " + e.Student.LastName, e.Course.Title)).ToListAsync(ct);
    public Task<List<EnrollmentDto>> ByCourseAsync(int id, CancellationToken ct) => db.Enrollments.AsNoTracking()
        .Where(e => e.CourseId == id).OrderBy(e => e.StudentId)
        .Select(e => new EnrollmentDto(e.StudentId, e.CourseId, e.Status, e.Grade, e.EnrollmentDateUtc,
            e.Student.FirstName + " " + e.Student.LastName, e.Course.Title)).ToListAsync(ct);
    private static EnrollmentDto Map(Enrollment e) => new(e.StudentId, e.CourseId, e.Status, e.Grade, e.EnrollmentDateUtc);
}
