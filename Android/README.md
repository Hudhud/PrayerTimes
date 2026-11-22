# Salah Times til Android

Android-appen viser dagens bønnetider, en live nedtælling og en månedskalender, hvor dagene kan foldes ud. Søg efter en by eller et postnummer under **Skift sted**. Under **Indstillinger** kan du vælge standard- eller Hanafi-metoden og læse beregningsvejledningen.

Appen bruger de samme bønneberegninger og den samme stedliste som salahtimes.dk. Tiderne beregnes på telefonen og vises i dansk lokal tid. Appen gemmer senest valgte steder og Asr-metoden lokalt. Stedlisten er baseret på GeoNames og deles under CC BY 4.0.

Vælg **Indstillinger → Tilføj 2 × 4-widget**, eller find **Salah Times 2 × 4** i Androids widgetmenu på startskærmen. Widgetten viser dagens tider, solopgang og næste bøn, følger sted og Asr-metode fra appen og åbner appen ved tryk.

## Byg APK’en

Brug .NET 10 med Android-arbejdsbelastningen, JDK 21 og Android SDK Platform 36.

```sh
dotnet workload install android
dotnet publish Android/SalahTimes.Android.csproj -c Release
```

APK’en findes under `Android/bin/Release/net10.0-android36.0/`. Den kræver Android 8.0 eller nyere. Brug samme signeringsnøgle og en højere versionskode til opdateringer af en installeret APK.

Websitet og de delte beregningstests kan bygges som før med `dotnet test PrayerTimes.sln -c Release`.
