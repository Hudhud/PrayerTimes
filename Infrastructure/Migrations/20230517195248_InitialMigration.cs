using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "CityPrayerTimes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    City = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CityPrayerTimes", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "DailyPrayerTimes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Date = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    FajrTime = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    SunriseTime = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    DhuhrTime = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    AsrTime = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    AsrHanafiTime = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    MaghribTime = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    IshaTime = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CityPrayerTimesId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyPrayerTimes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DailyPrayerTimes_CityPrayerTimes_CityPrayerTimesId",
                        column: x => x.CityPrayerTimesId,
                        principalTable: "CityPrayerTimes",
                        principalColumn: "Id");
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_DailyPrayerTimes_CityPrayerTimesId",
                table: "DailyPrayerTimes",
                column: "CityPrayerTimesId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DailyPrayerTimes");

            migrationBuilder.DropTable(
                name: "CityPrayerTimes");
        }
    }
}
