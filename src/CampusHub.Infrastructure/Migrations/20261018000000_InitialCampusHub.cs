using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace CampusHub.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261018000000_InitialCampusHub")]
public sealed class InitialCampusHub : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.CreateTable("Students", table => new
        {
            Id = table.Column<int>(type: "integer", nullable: false).Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
            FirstName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
            LastName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
            Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
            NormalizedEmail = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
            GroupName = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
            Age = table.Column<int>(type: "integer", nullable: true),
            LegacyAverageGrade = table.Column<decimal>(type: "numeric(5,2)", nullable: true)
        }, constraints: table =>
        {
            table.PrimaryKey("PK_Students", x => x.Id);
            table.CheckConstraint("CK_Student_Age", "\"Age\" IS NULL OR \"Age\" BETWEEN 1 AND 120");
            table.CheckConstraint("CK_Student_Avg", "\"LegacyAverageGrade\" IS NULL OR \"LegacyAverageGrade\" BETWEEN 0 AND 100");
        });
        m.CreateTable("Courses", table => new
        {
            Id = table.Column<int>(type: "integer", nullable: false).Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
            Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
            Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
            DurationHours = table.Column<int>(type: "integer", nullable: true),
            Capacity = table.Column<int>(type: "integer", nullable: false)
        }, constraints: table =>
        {
            table.PrimaryKey("PK_Courses", x => x.Id);
            table.CheckConstraint("CK_Course_Capacity", "\"Capacity\" > 0");
            table.CheckConstraint("CK_Course_Duration", "\"DurationHours\" IS NULL OR \"DurationHours\" > 0");
        });
        m.CreateTable("Enrollments", table => new
        {
            StudentId = table.Column<int>(type: "integer", nullable: false),
            CourseId = table.Column<int>(type: "integer", nullable: false),
            Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
            Grade = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
            EnrollmentDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
        }, constraints: table =>
        {
            table.PrimaryKey("PK_Enrollments", x => new { x.StudentId, x.CourseId });
            table.ForeignKey("FK_Enrollments_Students_StudentId", x => x.StudentId, "Students", "Id", onDelete: ReferentialAction.Cascade);
            table.ForeignKey("FK_Enrollments_Courses_CourseId", x => x.CourseId, "Courses", "Id", onDelete: ReferentialAction.Cascade);
            table.CheckConstraint("CK_Enrollment_Grade", "\"Grade\" IS NULL OR \"Grade\" BETWEEN 0 AND 100");
        });
        m.CreateIndex("IX_Students_NormalizedEmail", "Students", "NormalizedEmail", unique: true);
        m.CreateIndex("IX_Enrollments_CourseId", "Enrollments", "CourseId");
    }
    protected override void Down(MigrationBuilder m)
    {
        m.DropTable("Enrollments"); m.DropTable("Courses"); m.DropTable("Students");
    }
}
