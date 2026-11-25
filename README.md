# Salah Times

Danish prayer timetables with city and postcode search. The ASP.NET Core site uses .NET 10 and local astronomical calculations. All dates and displayed times use Copenhagen time.

## Run locally

Install the .NET SDK from [global.json](global.json).

```sh
dotnet restore PrayerTimes.sln --locked-mode
dotnet run --project Web
```

## Verify

```sh
dotnet test PrayerTimes.sln --configuration Release
```

The regression fixture covers six places and selected calendar and bright-night boundaries from the 2026 cutover (`dc61543`). Its expected times reflect the change from a fixed three-minute addition to rounding up to a whole minute. Use separate source timetables when evaluating an intentional change to calculation rules.

Run the browser checks after a Release build.

```sh
cd Tests/Browser
npm ci
npx playwright install --with-deps chromium webkit
npm test
```

## Deploy

The deployment publishes a self-contained Windows x86 build for the ASP.NET Core IIS hosting module. The website uses the shipped place index and caches timetables by coordinates, date, and Fajr and Isha angles. Logs go outside `wwwroot` and stay out of Git and the publish bundle.

After deployment, CI opens the public site and verifies the prayer times, postcode picker and calculation method. To roll back one release, revert its squash commit through a pull request so verification and deployment run again. A revert leaves the public place URLs and preference cookies usable by the earlier local release.

## Evaluate local prayer-time calculations

Astronomy Engine powers the shared calculation in `Calculation`. The Android app runs it on the device and the website runs it on the server. The calculator places Dhuhr after the solar disk clears the meridian. Both Asr conventions use apparent solar altitudes for the reference shadow at noon and the afternoon shadow. Fajr and Isha use configurable twilight angles, defaulting to 18° and 17°, with a bright-night flag when a crossing is absent. Calculated times round up to the next whole minute when they include fractional minutes.

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

The report lists coverage per city and month, availability differences, and signed differences in minutes. Positive values mean the calculation is later than the reference. The 2026 cutover comparison covered all 1,460 city-days for the four established cities. Displayed times differed by at most one minute and both providers agreed on the dates with missing Fajr or Isha. Repeat the comparison when changing calculation rules.

## Local place search

Place search uses the installed place snapshot.

The place index is a filtered, combined copy of the GeoNames [Danish place export](https://download.geonames.org/export/dump/DK.zip) and [Danish postal export](https://download.geonames.org/export/zip/DK.zip), used under [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/). The four established URLs use their existing coordinates. The snapshot can be refreshed with the source ZIP files.

```sh
python3 Tools/PlaceData/import_geonames.py places.zip postal.zip Web/wwwroot/data/prayer-places.json
```

City and postal-code suggestions use the server's copy of the index. For geolocation, the browser loads the index and opens the nearest named place's shareable URL. After the user enables that mode, the browser can update the place when they return, while it still has location permission. The user can turn off automatic location or choose a place manually.
