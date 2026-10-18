using CampusHub.Api.Services;
using CampusHub.Domain;
using CampusHub.Infrastructure;
using CampusHub.Reporting;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Newtonsoft.Json.Converters;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers().AddNewtonsoftJson(o => o.SerializerSettings.Converters.Add(new StringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
var cs = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(cs)) throw new InvalidOperationException("Missing ConnectionStrings:DefaultConnection. Set it via environment variable or user-secrets.");
builder.Services.AddDbContext<AppDbContext>(o => o.UseNpgsql(cs));
builder.Services.AddScoped<StudentService>();
builder.Services.AddScoped<CourseService>();
builder.Services.AddScoped<EnrollmentService>();
builder.Services.AddScoped<LegacyReportsService>();


var app = builder.Build();
app.UseExceptionHandler(handler => handler.Run(async context =>
{
    var error = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
    var code = error switch
    {
        DomainProblem d => d.StatusCode,
        DbUpdateException { InnerException: PostgresException { SqlState: "23505" } } => StatusCodes.Status409Conflict,
        DbUpdateException { InnerException: PostgresException { SqlState: "23514" } } => StatusCodes.Status400BadRequest,
        _ => StatusCodes.Status500InternalServerError
    };
    if (code == 500) app.Logger.LogError(error, "Unhandled API failure");
    context.Response.StatusCode = code;
    await Results.Problem(title: code == 500 ? "Internal server error" : error is DomainProblem ? error.Message : "Database constraint violated",
        statusCode: code).ExecuteAsync(context);
}));
if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }
app.MapGet("/health", async (AppDbContext db, CancellationToken ct) => await db.Database.CanConnectAsync(ct) ? Results.Ok(new { status = "healthy" }) : Results.StatusCode(503));
app.MapControllers();

if (builder.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    if (builder.Configuration.GetValue<bool>("SeedData:Enabled")) await DemoSeeder.SeedAsync(db);
}

app.Run();

public partial class Program { }
