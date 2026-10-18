using CampusHub.Domain;
using Microsoft.EntityFrameworkCore;

namespace CampusHub.Infrastructure;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Student> Students => Set<Student>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<Enrollment> Enrollments => Set<Enrollment>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Student>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.FirstName).HasMaxLength(100).IsRequired();
            e.Property(x => x.LastName).HasMaxLength(100).IsRequired();
            e.Property(x => x.Email).HasMaxLength(254).IsRequired();
            e.Property(x => x.NormalizedEmail).HasMaxLength(254).IsRequired();
            e.HasIndex(x => x.NormalizedEmail).IsUnique();
            e.Property(x => x.GroupName).HasMaxLength(60);
            e.Property(x => x.LegacyAverageGrade).HasPrecision(5, 2);
            e.ToTable(t => { t.HasCheckConstraint("CK_Student_Age", "\"Age\" IS NULL OR \"Age\" BETWEEN 1 AND 120"); t.HasCheckConstraint("CK_Student_Avg", "\"LegacyAverageGrade\" IS NULL OR CAST(\"LegacyAverageGrade\" AS NUMERIC) BETWEEN 0 AND 100"); });
        });
        b.Entity<Course>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
            e.Property(x => x.Description).HasMaxLength(2000).IsRequired();
            e.ToTable(t => { t.HasCheckConstraint("CK_Course_Capacity", "\"Capacity\" > 0"); t.HasCheckConstraint("CK_Course_Duration", "\"DurationHours\" IS NULL OR \"DurationHours\" > 0"); });
        });
        b.Entity<Enrollment>(e =>
        {
            e.HasKey(x => new { x.StudentId, x.CourseId });
            e.HasIndex(x => x.CourseId);
            e.HasOne(x => x.Student).WithMany(x => x.Enrollments).HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Course).WithMany(x => x.Enrollments).HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.Cascade);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.Grade).HasPrecision(5, 2);
            e.ToTable(t => t.HasCheckConstraint("CK_Enrollment_Grade", "\"Grade\" IS NULL OR CAST(\"Grade\" AS NUMERIC) BETWEEN 0 AND 100"));
        });
    }
}
