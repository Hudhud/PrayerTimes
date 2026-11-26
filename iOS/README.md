# Salah Times for iPhone

The UIKit app uses the same prayer calculations and GeoNames catalogs as Android and Salahtimes.dk. It starts in Danish, has an English switch in Settings, and handles today’s prayers, the monthly timetable and city search offline. It displays prayer times in the selected city’s time zone.

Prayer notifications are local. Depending on the enabled options, the app schedules up to 60 upcoming reminders and refreshes the queue when it opens. Missing solar events are omitted from reminders.

## Build on macOS

The repository's [iOS workflow](../.github/workflows/ios.yml) builds with Xcode 26.6 and a compatible .NET 10 iOS workload set. It launches the app on an iPhone simulator and attaches the simulator app and a screenshot.

```sh
dotnet workload install ios --version 10.0.401
dotnet build iOS/SalahTimes.iOS.csproj -c Release -p:RuntimeIdentifier=iossimulator-arm64 \
  -p:EnableCodeSigning=false -o /tmp/salahtimes-build
bash iOS/sign-simulator.sh /tmp/salahtimes-build/SalahTimes.iOS.app
```

[Microsoft's workload releases](https://github.com/dotnet/macios/releases) list the compatible workload set for each Xcode version.

## TestFlight

Register the bundle identifier `dk.salahtimes.ios` and create the app record in App Store Connect. Use the Apple Developer team’s distribution certificate and App Store provisioning profile to publish the device build on macOS. Pass the certificate and profile identities for your team to .NET.

```sh
dotnet publish iOS/SalahTimes.iOS.csproj -c Release -p:RuntimeIdentifier=ios-arm64 \
  -p:ArchiveOnBuild=true -p:BuildIpa=true \
  '-p:CodesignKey=Apple Distribution: TEAM NAME (TEAM_ID)' \
  '-p:CodesignProvision=Salah Times App Store'
```

Upload the signed IPA with Transporter. After Apple processes it, add it to an internal TestFlight group and check the language switch, worldwide city search, offline timetable and notification delivery on a physical iPhone.
