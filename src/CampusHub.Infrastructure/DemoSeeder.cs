using System.Globalization;
using CampusHub.Domain;
using Microsoft.EntityFrameworkCore;

namespace CampusHub.Infrastructure;

public static class DemoSeeder
{
    public static async Task SeedAsync(AppDbContext db, CancellationToken ct = default)
    {
        if (await db.Students.AnyAsync(ct)) return;
        var data = Path.Combine(AppContext.BaseDirectory, "Seed", "students.txt");
        if (!File.Exists(data)) throw new FileNotFoundException("Missing bundled legacy students seed", data);
        var students = new List<Student>();
        foreach (var line in await File.ReadAllLinesAsync(data, ct))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var p = line.Split(';');
            if (p.Length != 5 || !int.TryParse(p[0], out var id) || !int.TryParse(p[3], out var age) ||
                !decimal.TryParse(p[4].Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var grade))
                throw new FormatException($"Invalid legacy seed row: {line}");
            var split = p[1].Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            var email = $"legacy-{id}@campushub.invalid";
            students.Add(new Student { FirstName = split[0], LastName = split.Length > 1 ? split[1] : "Unknown", GroupName = p[2], Age = age,
                LegacyAverageGrade = grade, Email = email, NormalizedEmail = email.ToUpperInvariant() });
        }
        await db.Students.AddRangeAsync(students, ct);
        await db.Courses.AddRangeAsync(new[]
        {
            new Course { Title = "Databases", Description = "Relational modelling and SQL", DurationHours = 60, Capacity = 25 },
            new Course { Title = "Algorithms", Description = "Algorithms and data structures", DurationHours = 70, Capacity = 30 },
            new Course { Title = "Computer Graphics", Description = "Linear algebra and rendering", DurationHours = 50, Capacity = 20 }
        }, ct);
        await db.SaveChangesAsync(ct);
    }
}
