using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Daily_Prayer_Times_Relationship : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DailyPrayerTimes_CityPrayerTimes_CityPrayerTimesId",
                table: "DailyPrayerTimes");

            migrationBuilder.AlterColumn<int>(
                name: "CityPrayerTimesId",
                table: "DailyPrayerTimes",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_DailyPrayerTimes_CityPrayerTimes_CityPrayerTimesId",
                table: "DailyPrayerTimes",
                column: "CityPrayerTimesId",
                principalTable: "CityPrayerTimes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DailyPrayerTimes_CityPrayerTimes_CityPrayerTimesId",
                table: "DailyPrayerTimes");

            migrationBuilder.AlterColumn<int>(
                name: "CityPrayerTimesId",
                table: "DailyPrayerTimes",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddForeignKey(
                name: "FK_DailyPrayerTimes_CityPrayerTimes_CityPrayerTimesId",
                table: "DailyPrayerTimes",
                column: "CityPrayerTimesId",
                principalTable: "CityPrayerTimes",
                principalColumn: "Id");
        }
    }
}
