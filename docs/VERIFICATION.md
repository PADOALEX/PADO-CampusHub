# Verification status (2026-10-18)

This document distinguishes **verified CI checks** from items that have not been exercised. The public repository is [PADOALEX/PADO-CampusHub](https://github.com/PADOALEX/PADO-CampusHub).

**Passing CI evidence:** [.NET 10 CI run #8](https://github.com/PADOALEX/PADO-CampusHub/actions/runs/39129629321), commit `751d3444b4cce1626cbed145b905b25e7e9d46f0`. GitHub Actions concluded `success`.

| Check | Observed outcome |
| --- | --- |
| All three original archives audited and integrated | Source migration audit recorded |
| Single .NET 10 solution and shared EF Core data model | Build succeeds |
| NuGet restore and Release build on Ubuntu GitHub runner | **Passed** |
| xUnit unit tests (SQLite relational in-memory) | **Passed: 6/6** |
| HTTP integration tests (WebApplicationFactory + SQLite) | **Passed: 2/2** |
| PostgreSQL 17 service starts in GitHub Actions | **Passed** |
| EF Core initial migration against PostgreSQL 17 | **Passed** |
| Demo seeding and API HTTP smoke checks | **Passed** |
| Docker Compose full two-container startup on Windows | **Not yet independently verified** |
| Parallel PostgreSQL enrollment stress test | **Not yet performed** |
| Production authentication, authorization, backups and data privacy controls | **Out of educational-project scope** |

## Improvements from the initial CI failure

- Aligned Microsoft EF Core package versions to prevent CS1705.
- Updated the OpenAPI and SQLite bundle dependencies identified by NuGet vulnerability warnings.
- Updated `actions/checkout` and `actions/setup-dotnet` from v4 to v5.
- Fixed MVC validation metadata on enrollment request DTOs.
- Reworked reporting and filtering for server-side EF Core query translation.
- Corrected the `AppDbContext` snapshot using `dotnet-ef`, keeping the EF migration integrity check enabled.

## Portfolio deployment note

The CI pipeline verifies compilation, test behaviour and a single PostgreSQL startup/migration/seed/API request path. It is not a substitute for production-level security review, operational monitoring, independent Docker Compose verification, backup/restore testing or realistic concurrent load testing. Do not deploy this educational backend as a public personal-data service without implementing these safeguards.
