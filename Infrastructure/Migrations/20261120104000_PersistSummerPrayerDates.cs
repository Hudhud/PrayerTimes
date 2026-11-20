using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations;

public partial class PersistSummerPrayerDates : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Keep both additions in one DDL statement on MySQL and MariaDB.
        migrationBuilder.Sql("""
            ALTER TABLE DailyPrayerTimes
                ADD COLUMN IshaDayOffset int NULL,
                ADD COLUMN UsesPredefinedFajrTime tinyint(1) NULL
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE DailyPrayerTimes
                DROP COLUMN IshaDayOffset,
                DROP COLUMN UsesPredefinedFajrTime
            """);
    }
}
