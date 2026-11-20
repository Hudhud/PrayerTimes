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

The worker prepares the current and next month in the background. An empty database can therefore take a few minutes to populate. City rules live in [PrayerCities.cs](Application/Calendars/PrayerCities.cs). Provider values and summer exceptions are converted and validated in [PrayerTimeService.cs](Infrastructure/Services/PrayerTimeService.cs). The website reads saved rows directly. During bright nights, it displays dashes for Fajr or Isha when there is no regular time for that prayer.

## Verify

```sh
dotnet test PrayerTimes.sln --configuration Release
```

Set `PRAYERTIMES_TEST_DATABASE` to a dedicated MariaDB test database to include the relational test. It migrates that database and tests concurrent writes and rollback. GitHub Actions runs both the unit and MariaDB tests before deployment.

## Deploy

Set `ConnectionStrings__DefaultConnection` or `CONNECTION_STRING` in the IIS hosting environment. The workflow no longer generates a production settings file. An existing host-managed `appsettings.Production.json` can serve as a transitional fallback while the environment value is set. The deployment remains a self-contained Windows x86 publish and expects the ASP.NET Core IIS hosting module.

The upgrade adds two nullable timetable columns. Existing rows remain readable and the worker refreshes their metadata as needed. Logs go outside `wwwroot` and stay out of Git and the publish bundle.

## Evaluate local prayer-time calculations

The offline prototype uses Astronomy Engine in `Calculation`. It calculates sunrise, sunset, solar noon, both Asr shadow conventions, and separate twilight crossings for Fajr and Isha. A missing twilight crossing produces an empty time with the corresponding bright-night flag. The website still uses its existing timetable provider during evaluation.

Run the comparison against saved reference times that use Salah Times' current city coordinates and displayed time conventions.

```sh
dotnet run --project Tools/PrayerTimeComparison --configuration Release -- reference.json
```

The JSON file contains an array of objects with these fields.

| Field | Format |
| --- | --- |
| `city` | City slug from `PrayerCities.cs` |
| `date` | Danish calendar date in `yyyy-MM-dd` |
| `sunrise`, `dhuhr`, `asr`, `asrHanafi`, `maghrib` | Displayed Danish time in `HH:mm` |
| `fajr`, `isha` | Displayed time in `HH:mm`, or `null` when the site shows dashes |
| `ishaDayOffset` | `0` on the row's date, `1` after midnight, or `null` when Isha has no regular time |

The report lists coverage per city and month, availability differences, and signed differences in minutes. Positive values mean the calculation is later than the reference. The release review should cover a full year for the four existing cities, including the start and end of bright nights, daylight-saving transitions, after-midnight Isha, and any systematic differences around solar noon or the Asr shadow thresholds.
