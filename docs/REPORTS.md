# Legacy LINQ report fidelity

All 12 original operations from `LinqStudentsProject/Program.cs` are exposed in the single response from `GET /api/reports/legacy?group=PMA-21&limit=1000`.

| # | Original LINQ operation | New response field | SQL / semantics |
| --- | --- | --- | --- |
| 1 | `Where(s.Group.Equals(target, ignoreCase))` | `selectedGroupStudents` | Case-insensitive group filter |
| 2 | `Where(s.AverageGrade > 85)` | `above85` | Average strictly over 85 |
| 3 | `OrderByDescending(AverageGrade)` | `sortedByGrade` | Descending average, ID tie-break |
| 4 | `Select(FullName)` | `studentNames` | Projection of FirstName + LastName |
| 5 | `GroupBy(Group).Count()` | `studentsCountByGroup` | Includes count and average; also present in groupStatistics |
| 6 | `MaxBy(AverageGrade)` | `bestStudent` | Highest non-null average, ID tie-break |
| 7 | `Any(Age < 18)` | `hasUnderageStudents` + `underageStudents` | Filter for known ages below 18 |
| 8 | `Distinct(Group)` | `uniqueGroups` | Distinct, alphabetically ordered |
| 9 | `OrderByDescending(...).Take(3)` | `top3` | Three highest averages |
| 10 | `Average(AverageGrade)` | `overallAverage` | AVG across known effective averages |
| 11 | `GroupBy(Group).Select(Count, Average)` | `groupStatistics` | SQL group aggregation |
| 12 | `groupStats.MaxBy(AverageGroupGrade)` | `bestGroup` | Best group by mean, alphabetical tie-break |

**Compatibility notes:** Historical `students.txt` grades do not belong to any course. New system stores them separately as `Student.LegacyAverageGrade` and derives an **effective average** from completed and graded enrollments when possible. That rule is deterministic and preserves source data before students complete courses. Null averages (new students without grade or historical score) are excluded from highest-grade competition and overall average; groups with all null averages have a null group mean.

**Bounded output:** List sections are limited by `limit` (3–10,000, default 1,000), unlike the original full-list lab. `isTruncated` reports when the total student/group population is beyond the limit. For full exports over 10,000 rows, use controlled paging via the `/api/students` route or implement a dedicated streaming export.

The API also exposes five original CLI analytics queries: `GET /api/enrollments/student/{id}`, `GET /api/enrollments/course/{id}`, `GET /api/reports/course-stats`, `GET /api/students?olderThan=...`, and `GET /api/courses?longerThan=...`.
