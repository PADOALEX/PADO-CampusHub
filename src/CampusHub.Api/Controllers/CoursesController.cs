using CampusHub.Api.Services;
using CampusHub.Domain;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace CampusHub.Api.Controllers;

[ApiController, Route("api/courses")]
public sealed class CoursesController(CourseService service) : ControllerBase
{
    [HttpGet]
    public Task<PageDto<CourseDto>> List([FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery, Range(1, 100)] int pageSize = 20, string? search = null, int? longerThan = null,
        string? sort = null, bool descending = false, CancellationToken ct = default) =>
        service.SearchAsync(page, pageSize, search, longerThan, sort, descending, ct);
    [HttpGet("{id:int}")]
    public async Task<ActionResult<CourseDto>> Get(int id, CancellationToken ct)
    {
        var found = await service.GetAsync(id, ct); return found is null ? NotFound() : Ok(found);
    }
    [HttpPost]
    public async Task<ActionResult<CourseDto>> Create([FromBody] CourseWriteRequest dto, CancellationToken ct)
    {
        var value = await service.CreateAsync(dto, ct); return CreatedAtAction(nameof(Get), new { id = value.Id }, value);
    }
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] CourseWriteRequest dto, CancellationToken ct) =>
        await service.UpdateAsync(id, dto, ct) ? NoContent() : NotFound();
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct) => await service.DeleteAsync(id, ct) ? NoContent() : NotFound();
}
