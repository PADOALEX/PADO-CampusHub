namespace CampusHub.Domain;

public sealed class Student
{
    public int Id { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Email { get; set; } = "";
    public string NormalizedEmail { get; set; } = "";
    public string? GroupName { get; set; }
    public int? Age { get; set; }
    // Preserves the legacy students.txt average; not the grade of any specific course.
    public decimal? LegacyAverageGrade { get; set; }
    public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
}

public sealed class Course
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public int? DurationHours { get; set; }
    public int Capacity { get; set; } = 30;
    public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
}

public enum EnrollmentStatus { Active = 0, Completed = 1, Cancelled = 2 }

public sealed class Enrollment
{
    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;
    public int CourseId { get; set; }
    public Course Course { get; set; } = null!;
    public EnrollmentStatus Status { get; set; } = EnrollmentStatus.Active;
    public decimal? Grade { get; set; }
    public DateTime EnrollmentDateUtc { get; set; } = DateTime.UtcNow;
}
