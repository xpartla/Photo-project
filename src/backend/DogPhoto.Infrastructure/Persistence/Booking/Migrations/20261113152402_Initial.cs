using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DogPhoto.Infrastructure.Persistence.Booking.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "booking");

            migrationBuilder.CreateTable(
                name: "availability_slots",
                schema: "booking",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    IsBlocked = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_availability_slots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "session_types",
                schema: "booking",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    NameSk = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    NameEn = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Slug = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    DescriptionSk = table.Column<string>(type: "text", nullable: true),
                    DescriptionEn = table.Column<string>(type: "text", nullable: true),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: false),
                    BasePrice = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false, defaultValue: "EUR"),
                    Category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IncludesJson = table.Column<string>(type: "jsonb", nullable: true),
                    MaxDogs = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_session_types", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "bookings",
                schema: "booking",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    SessionTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    SlotId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "Pending"),
                    ClientName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ClientEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ClientPhone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    DogCount = table.Column<int>(type: "integer", nullable: false),
                    SpecialRequests = table.Column<string>(type: "text", nullable: true),
                    TotalPrice = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    DepositPaid = table.Column<bool>(type: "boolean", nullable: false),
                    GoPayPaymentId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bookings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_bookings_availability_slots_SlotId",
                        column: x => x.SlotId,
                        principalSchema: "booking",
                        principalTable: "availability_slots",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_bookings_session_types_SessionTypeId",
                        column: x => x.SessionTypeId,
                        principalSchema: "booking",
                        principalTable: "session_types",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_bookings_SessionTypeId",
                schema: "booking",
                table: "bookings",
                column: "SessionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_bookings_SlotId",
                schema: "booking",
                table: "bookings",
                column: "SlotId");

            migrationBuilder.CreateIndex(
                name: "IX_session_types_Slug",
                schema: "booking",
                table: "session_types",
                column: "Slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bookings",
                schema: "booking");

            migrationBuilder.DropTable(
                name: "availability_slots",
                schema: "booking");

            migrationBuilder.DropTable(
                name: "session_types",
                schema: "booking");
        }
    }
}
