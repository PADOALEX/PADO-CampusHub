using System.ComponentModel.DataAnnotations;

namespace CampusHub.Domain;

public sealed class StudentWriteRequest
{
    [Required, StringLength(100)] public string FirstName { get; set; } = "";
    [StringLength(100)] public string LastName { get; set; } = "";
    [Required, EmailAddress, StringLength(254)] public string Email { get; set; } = "";
    [StringLength(60)] public string? GroupName { get; set; }
    [Range(1, 120)] public int? Age { get; set; }
    [Range(typeof(decimal), "0", "100")] public decimal? LegacyAverageGrade { get; set; }
}
public sealed record StudentDto(int Id, string FirstName, string LastName, string Email, string? GroupName, int? Age, decimal? AverageGrade);

public sealed class CourseWriteRequest
{
    [Required, StringLength(200)] public string Title { get; set; } = "";
    [StringLength(2000)] public string Description { get; set; } = "";
    [Range(1, 10000)] public int? DurationHours { get; set; }
    [Range(1, 100000)] public int Capacity { get; set; } = 30;
}
public sealed record CourseDto(int Id, string Title, string Description, int? DurationHours, int Capacity, int ActiveStudents);
public sealed record EnrollmentRequest([property: Range(1, int.MaxValue)] int StudentId, [property: Range(1, int.MaxValue)] int CourseId);
public sealed record GradeRequest([property: Range(typeof(decimal), "0", "100")] decimal Grade);
public sealed record StatusRequest(EnrollmentStatus Status);
public sealed record EnrollmentDto(int StudentId, int CourseId, EnrollmentStatus Status, decimal? Grade, DateTime EnrollmentDateUtc, string? StudentName = null, string? CourseTitle = null);
public sealed record PageDto<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

public sealed record ScoreDto(int Id, string FullName, string? GroupName, int? Age, decimal? AverageGrade);
public sealed record GroupStatsDto(string GroupName, int StudentCount, decimal? AverageGrade);
public sealed record CourseStatsDto(int Id, string Title, int ActiveStudents, int Capacity);
public sealed record LegacyReportDto(
    IReadOnlyList<ScoreDto> SelectedGroupStudents,
    IReadOnlyList<ScoreDto> Above85,
    IReadOnlyList<ScoreDto> SortedByGrade,
    IReadOnlyList<string> StudentNames,
    IReadOnlyList<GroupStatsDto> StudentsCountByGroup,
    ScoreDto? BestStudent,
    bool HasUnderageStudents,
    IReadOnlyList<ScoreDto> UnderageStudents,
    IReadOnlyList<string> UniqueGroups,
    IReadOnlyList<ScoreDto> Top3,
    decimal? OverallAverage,
    IReadOnlyList<GroupStatsDto> GroupStatistics,
    GroupStatsDto? BestGroup,
    bool IsTruncated,
    int RowLimit);

public sealed class DomainProblem(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}
