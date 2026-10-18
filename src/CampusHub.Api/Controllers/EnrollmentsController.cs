using CampusHub.Api.Services;
using CampusHub.Domain;
using Microsoft.AspNetCore.Mvc;

namespace CampusHub.Api.Controllers;

[ApiController, Route("api/enrollments")]
public sealed class EnrollmentsController(EnrollmentService service) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<EnrollmentDto>> Enroll([FromBody] EnrollmentRequest dto, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await service.EnrollAsync(dto, ct));
    [HttpGet("student/{studentId:int}")]
    public Task<List<EnrollmentDto>> ByStudent(int studentId, CancellationToken ct) => service.ByStudentAsync(studentId, ct);
    [HttpGet("course/{courseId:int}")]
    public Task<List<EnrollmentDto>> ByCourse(int courseId, CancellationToken ct) => service.ByCourseAsync(courseId, ct);
    [HttpPut("{studentId:int}/{courseId:int}/grade")]
    public async Task<IActionResult> Grade(int studentId, int courseId, [FromBody] GradeRequest dto, CancellationToken ct) =>
        await service.SetGradeAsync(studentId, courseId, dto.Grade, ct) ? NoContent() : NotFound();
    [HttpPut("{studentId:int}/{courseId:int}/status")]
    public async Task<IActionResult> SetStatus(int studentId, int courseId, [FromBody] StatusRequest dto, CancellationToken ct) =>
        await service.SetStatusAsync(studentId, courseId, dto.Status, ct) ? NoContent() : NotFound();
    [HttpDelete("{studentId:int}/{courseId:int}")]
    public async Task<IActionResult> Cancel(int studentId, int courseId, CancellationToken ct) =>
        await service.SetStatusAsync(studentId, courseId, EnrollmentStatus.Cancelled, ct) ? NoContent() : NotFound();
}
