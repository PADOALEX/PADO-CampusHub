using CampusHub.Api.Services;
using CampusHub.Domain;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using CampusHub.Infrastructure;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace CampusHub.Api.Controllers;

[ApiController, Route("api/students")]
public sealed class StudentsController(StudentService service, AppDbContext db) : ControllerBase
{
    [HttpGet]
    public Task<PageDto<StudentDto>> List([FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery, Range(1, 100)] int pageSize = 20, string? search = null, string? group = null,
        int? olderThan = null, string? sort = null, bool descending = false, CancellationToken ct = default) =>
        service.SearchAsync(page, pageSize, search, group, olderThan, sort, descending, ct);

    [HttpGet("{id:int}")]
    public async Task<ActionResult<StudentDto>> Get(int id, CancellationToken ct)
    {
        var found = await service.GetAsync(id, ct);
        return found is null ? NotFound() : Ok(found);
    }
    [HttpPost]
    public async Task<ActionResult<StudentDto>> Create([FromBody] StudentWriteRequest dto, CancellationToken ct)
    {
        var value = await service.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(Get), new { id = value.Id }, value);
    }
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] StudentWriteRequest dto, CancellationToken ct) =>
        await service.UpdateAsync(id, dto, ct) ? NoContent() : NotFound();

    // Safe RFC6902 patch: only explicitly allowed writable properties; validate the result.
    [HttpPatch("{id:int}")]
    public async Task<IActionResult> Patch(int id, [FromBody] JsonPatchDocument<StudentWriteRequest> document, CancellationToken ct)
    {
        if (document is null || document.Operations.Count is < 1 or > 12) return BadRequest("Invalid patch document.");
        string[] fields = ["FirstName", "LastName", "Email", "Age", "GroupName", "LegacyAverageGrade"];
        if (document.Operations.Any(op => !new[] { "add", "replace", "remove" }.Contains(op.op.ToLowerInvariant()) ||
            !fields.Contains(op.path.TrimStart('/'), StringComparer.OrdinalIgnoreCase)))
            return BadRequest("Unsupported patch operation or property.");
        var student = await db.Students.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, ct);
        if (student is null) return NotFound();
        var current = new StudentWriteRequest
        {
            FirstName = student.FirstName, LastName = student.LastName, Email = student.Email,
            Age = student.Age, GroupName = student.GroupName, LegacyAverageGrade = student.LegacyAverageGrade
        };
        document.ApplyTo(current, ModelState);
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        var errors = new List<ValidationResult>();
        if (!Validator.TryValidateObject(current, new ValidationContext(current), errors, true))
            return BadRequest(errors.Select(e => e.ErrorMessage));
        return await service.UpdateAsync(id, current, ct) ? NoContent() : NotFound();
    }
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct) => await service.DeleteAsync(id, ct) ? NoContent() : NotFound();
}
