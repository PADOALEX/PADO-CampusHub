# Three-project source audit and migration map

Examined inputs (source, models, controllers, existing tests and LINQ operators):
`StudentCourseApi(2).zip`, `StudentCourseApp(2).zip`, `LinqStudentsProject(2).zip`.

| Original | Working capability | New location / decision |
| --- | --- | --- |
| `StudentCourseApi/StudentCourseApi/Models/Student.cs` | FirstName, LastName, Email, enrollments | `CampusHub.Domain.Student` |
| `StudentCourseApp/Models/Student.cs` | Name, Email, Age, enrollments | Split `Name` into FirstName/LastName; add optional Age |
| `LinqStudentsProject/Program.cs` Student | FullName, Group, Age, AverageGrade | `GroupName`, `Age`, `LegacyAverageGrade` on Student; computed grade in reports |
| Both API and CLI `Course.cs` | API Description versus CLI DurationHours | Keep BOTH plus Capacity in canonical Course |
| API and CLI `Enrollment.cs` | Composite student/course key, API timestamp | Keep composite PK, timestamp; add Grade and Status |
| API Controllers/StudentService | student CRUD (GET/POST/PUT/PATCH/DELETE) | `CampusHub.Api.Controllers.StudentsController`, `StudentService`; safe PATCH whitelist |
| API CourseService and CoursesController | course creation/list; unused service CRUD | `CoursesController`, `CourseService` full CRUD |
| API EnrollmentService/Controller | create, student courses, duplicate check in service | `EnrollmentsController` plus transaction-aware `EnrollmentService`; course students, cancel, grade, status |
| CLI `Program.cs` | add/list/details/update/delete/enroll and 5 queries | `CampusHub.Cli.Program` menu, but all calls use `HttpClient` |
| `LinqStudentsProject/Program.cs` | 12 classic LINQ report tasks | `CampusHub.Reporting.LegacyReportsService`; `/api/reports/legacy` |
| `LinqStudentsProject/students.txt` | 12 demo records | `CampusHub.Infrastructure/Seed/students.txt`, opt-in idempotent seed |
| API `Migrations/InitialCreate` | initial PostgreSQL schema | new initial migration for unified schema; **not** a destructive upgrade of existing DB |
| API `*.Tests` | xUnit/InMemory tests for service methods | rewritten SQLite relational unit/integration tests; API WebApplicationFactory tests |

## Notable conflicts and fixes

1. **Three incompatible student representations.** The canonical model uses FirstName/LastName, optional GroupName/Age and historical grade. Existing API clients can omit age/group/legacy average; old file data maps in without losing the original average.
2. **Different Course fields.** DurationHours and Description coexist; new Capacity supports actual registration limits.
3. **Two direct DB owners.** Legacy CLI hardcoded a PostgreSQL connection and even called `EnsureCreated()`, which clashes with EF migrations. Removed; API is the only DB owner.
4. **API controllers bypassed service logic.** Course and enrollment controllers directly manipulated `DbContext`, bypassing service validation and duplicate checking; the Program registered only student service. Fixed by route-to-service implementation and DI for each service.
5. **Old LINQ source was a file.** Reading all lines was fine for a small lab but unsuitable as an operational DB. Its values are used only for opt-in initial seed, not for live reporting.
6. **Old report's `AverageGrade` versus course `Grade`.** These are not equivalent. A nullable historical snapshot and a documented computed effective grade prevent conflating them.
7. **Secrets in input.** Both original appsettings and CLI context carried a literal PostgreSQL password. Those files are NOT copied into the result. If that password was real, rotate it immediately.
8. **Extra placeholder project in API ZIP.** The outer `StudentCourseApi/Program.cs` was a weather-forecast starter and was discarded; only the inner application contained the real domain API.
9. **Unexpected SQLite artifact.** `StudentCourseApp/school.db` contains **2 students, 1 course and 0 enrollments** despite the active code using PostgreSQL. It is deliberately excluded: no automatic data reconciliation, schema provenance or consent was supplied. Export existing data deliberately if needed.
10. **Legacy test gaps.** Original EF InMemory tests could not verify relational uniqueness, constraints and endpoints; now SQLite integration tests cover CRUD and HTTP conflicts, and a PostgreSQL smoke step is defined in CI.

## No hidden data migration

This release creates a NEW database schema. It **does not** auto-import records from old PostgreSQL or the archived SQLite file. `students.txt` demonstration data can be seeded explicitly and is clearly labelled as synthetic. Existing real data needs a separate vetted ETL with source backup, field mapping and collision policy.
