using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CampusHub.Domain;

Console.OutputEncoding = System.Text.Encoding.UTF8;
var baseUrl = args.FirstOrDefault(a => a.StartsWith("http", StringComparison.OrdinalIgnoreCase))
    ?? Environment.GetEnvironmentVariable("CAMPUSHUB_API_URL") ?? "http://localhost:8080";
using var http = new HttpClient { BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/"), Timeout = TimeSpan.FromSeconds(20) };
var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
json.Converters.Add(new JsonStringEnumConverter());

while (true)
{
    Console.WriteLine("\n=== PADO CampusHub | HTTP client ===");
    Console.WriteLine("1 Add student   2 Add course   3 View data   4 Update   5 Delete / cancel");
    Console.WriteLine("6 Enroll student   7 Queries   8 Grade / completion   9 Legacy LINQ report   0 Quit");
    Console.Write("Choice: ");
    var choice = Console.ReadLine();
    try
    {
        switch (choice)
        {
            case "0": return;
            case "1": await Call(HttpMethod.Post, "api/students", ReadStudent()); break;
            case "2": await Call(HttpMethod.Post, "api/courses", ReadCourse()); break;
            case "3": await View(); break;
            case "4": await Update(); break;
            case "5": await Delete(); break;
            case "6": await Call(HttpMethod.Post, "api/enrollments", new EnrollmentRequest(Positive("Student ID: "), Positive("Course ID: "))); break;
            case "7": await Queries(); break;
            case "8": await Grade(); break;
            case "9": await Call(HttpMethod.Get, "api/reports/legacy?group=" + Uri.EscapeDataString(Ask("Group (e.g. PMA-21): "))); break;
            default: Console.WriteLine("Unknown option."); break;
        }
    }
    catch (HttpRequestException ex) { Console.WriteLine($"API error: {ex.Message}"); }
    catch (TaskCanceledException) { Console.WriteLine("Request timed out; is the API running?"); }
    catch (JsonException ex) { Console.WriteLine($"Invalid JSON response: {ex.Message}"); }
    catch (Exception ex) { Console.WriteLine($"Input error: {ex.Message}"); }
}

async Task View()
{
    Console.WriteLine("1 Students   2 Courses   3 Enrollments for student   4 Enrollments for course   5 Student by ID   6 Course by ID");
    var option = Ask("Choice: ");
    var path = option switch
    {
        "1" => "api/students?pageSize=100",
        "2" => "api/courses?pageSize=100",
        "3" => $"api/enrollments/student/{Positive("Student ID: ")}",
        "4" => $"api/enrollments/course/{Positive("Course ID: ")}",
        "5" => $"api/students/{Positive("Student ID: ")}",
        "6" => $"api/courses/{Positive("Course ID: ")}",
        _ => ""
    };
    if (path.Length > 0) await Call(HttpMethod.Get, path);
}
async Task Update()
{
    Console.WriteLine("1 Student   2 Course");
    var type = Ask("Choice: ");
    if (type == "1") { var id = Positive("Student ID: "); await Call(HttpMethod.Put, $"api/students/{id}", ReadStudent()); }
    else if (type == "2") { var id = Positive("Course ID: "); await Call(HttpMethod.Put, $"api/courses/{id}", ReadCourse()); }
}
async Task Delete()
{
    Console.WriteLine("1 Student   2 Course   3 Cancel enrollment");
    var type = Ask("Choice: ");
    var path = type switch
    {
        "1" => $"api/students/{Positive("Student ID: ")}",
        "2" => $"api/courses/{Positive("Course ID: ")}",
        "3" => $"api/enrollments/{Positive("Student ID: ")}/{Positive("Course ID: ")}",
        _ => ""
    };
    if (path.Length > 0 && Ask("Type YES to confirm: ") == "YES") await Call(HttpMethod.Delete, path);
}
async Task Queries()
{
    Console.WriteLine("1 Courses for student   2 Students in course   3 Count students per course");
    Console.WriteLine("4 Students older than age   5 Courses longer than hours");
    var opt = Ask("Choice: ");
    var path = opt switch
    {
        "1" => $"api/enrollments/student/{Positive("Student ID: ")}",
        "2" => $"api/enrollments/course/{Positive("Course ID: ")}",
        "3" => "api/reports/course-stats",
        "4" => $"api/students?olderThan={Positive("Age: ")}&pageSize=100",
        "5" => $"api/courses?longerThan={Positive("Hours: ")}&pageSize=100",
        _ => ""
    };
    if (path.Length > 0) await Call(HttpMethod.Get, path);
}
async Task Grade()
{
    var student = Positive("Student ID: "); var course = Positive("Course ID: ");
    var grade = Decimal("Grade (0..100): ");
    if (grade is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(grade));
    await Call(HttpMethod.Put, $"api/enrollments/{student}/{course}/grade", new GradeRequest(grade));
    if (Ask("Mark completed? (y/n): ").Equals("y", StringComparison.OrdinalIgnoreCase))
        await Call(HttpMethod.Put, $"api/enrollments/{student}/{course}/status", new StatusRequest(EnrollmentStatus.Completed));
}
StudentWriteRequest ReadStudent() => new()
{
    FirstName = Ask("First name: "), LastName = Ask("Last name: "), Email = Ask("Email: "),
    GroupName = Optional("Group: "), Age = OptionalInt("Age: "), LegacyAverageGrade = OptionalDecimal("Historical average (optional): ")
};
CourseWriteRequest ReadCourse() => new()
{
    Title = Ask("Title: "), Description = Ask("Description: "),
    DurationHours = Positive("Duration hours: "), Capacity = Positive("Capacity: ")
};
async Task Call(HttpMethod method, string path, object? payload = null)
{
    using var req = new HttpRequestMessage(method, path);
    if (payload is not null) req.Content = JsonContent.Create(payload, options: json);
    using var res = await http.SendAsync(req);
    var body = await res.Content.ReadAsStringAsync();
    if (!res.IsSuccessStatusCode)
        throw new HttpRequestException($"{(int)res.StatusCode} {res.ReasonPhrase}: {body}", null, res.StatusCode);
    if (string.IsNullOrWhiteSpace(body)) { Console.WriteLine("Success."); return; }
    try { Console.WriteLine(JsonSerializer.Serialize(JsonSerializer.Deserialize<JsonElement>(body, json), json)); }
    catch (JsonException) { Console.WriteLine(body); }
}
string Ask(string prompt) { Console.Write(prompt); return Console.ReadLine()?.Trim() ?? ""; }
string? Optional(string prompt) { var value = Ask(prompt); return value.Length == 0 ? null : value; }
int Positive(string prompt)
{
    var raw = Ask(prompt);
    if (!int.TryParse(raw, out var n) || n < 1) throw new ArgumentException("Expected positive integer.");
    return n;
}
int? OptionalInt(string prompt)
{
    var raw = Optional(prompt);
    if (raw is null) return null;
    if (!int.TryParse(raw, out var n) || n < 1 || n > 120) throw new ArgumentException("Expected age 1..120.");
    return n;
}
decimal Decimal(string prompt)
{
    var raw = Ask(prompt).Replace(',', '.');
    if (!decimal.TryParse(raw, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var d))
        throw new ArgumentException("Expected decimal number.");
    return d;
}
decimal? OptionalDecimal(string prompt)
{
    var raw = Optional(prompt);
    if (raw is null) return null;
    if (!decimal.TryParse(raw.Replace(',', '.'), System.Globalization.NumberStyles.Number,
        System.Globalization.CultureInfo.InvariantCulture, out var d) || d is < 0 or > 100)
        throw new ArgumentException("Expected grade 0..100.");
    return d;
}
