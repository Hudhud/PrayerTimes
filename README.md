# Salah Times

Danish prayer timetables for København, Odense, Aarhus and Aalborg. The ASP.NET Core site uses .NET 10 and a MySQL-compatible database. All dates and displayed times use Copenhagen time.

## Run locally

Install the .NET SDK from [global.json](global.json) and create an empty MySQL or MariaDB database. Set the connection string in user secrets for development or in the hosting environment for production.

```sh
dotnet user-secrets set 'ConnectionStrings:DefaultConnection' 'Server=localhost;Database=salahtimes;User ID=YOUR_USER;Password=YOUR_PASSWORD' --project Web
dotnet restore PrayerTimes.sln --locked-mode
dotnet run --project Web
```

The app applies EF migrations at startup. To apply them separately, set `Database__ApplyMigrations=false` on the host and use a database identity with schema permissions for the migration command.

```sh
dotnet tool restore
dotnet ef database update --project Infrastructure --startup-project Web
```

The worker prepares the current and next month in the background. An empty database can therefore take a few minutes to populate. City rules live in [PrayerCities.cs](Application/Calendars/PrayerCities.cs). Provider values and summer exceptions are converted and validated in [PrayerTimeService.cs](Infrastructure/Services/PrayerTimeService.cs). The website reads saved rows directly.

## Verify

```sh
dotnet test PrayerTimes.sln --configuration Release
```

Set `PRAYERTIMES_TEST_DATABASE` to a dedicated MariaDB test database to include the relational test. It migrates that database and tests concurrent writes and rollback. GitHub Actions runs both the unit and MariaDB tests before deployment.

## Deploy

Set `ConnectionStrings__DefaultConnection` or `CONNECTION_STRING` in the IIS hosting environment. The workflow no longer generates a production settings file. An existing host-managed `appsettings.Production.json` can serve as a transitional fallback while the environment value is set. The deployment remains a self-contained Windows x86 publish and expects the ASP.NET Core IIS hosting module.

The upgrade adds two nullable timetable columns. Existing rows remain readable and the worker refreshes their metadata as needed. Logs go outside `wwwroot` and stay out of Git and the publish bundle.
