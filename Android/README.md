# Salah Times til Android

Android-appen viser dagens bønnetider, en live nedtælling og en månedskalender, hvor dagene kan foldes ud. Appen bruger telefonens placering til at vælge en nærliggende by. Ved en omtrentlig placering kan du bekræfte den foreslåede by. Du kan også søge efter en by eller et dansk postnummer under **Skift sted**. Appen beholder et manuelt valgt sted, indtil du vælger **Brug min placering** og appen finder et sted. Under **Indstillinger** kan du vælge dansk eller engelsk, vælge standard- eller Hanafi-metoden og ændre vinklerne for Fajr og Isha. Tryk på kalendermåneden for at vælge en bestemt dato.

Appen bruger de samme bønneberegninger og de samme stedlister som salahtimes.dk. Tiderne beregnes på telefonen og vises i det valgte steds lokale tid. Appen gemmer senest valgte steder og dine indstillinger lokalt. Steddata er baseret på GeoNames og deles under CC BY 4.0.

Med automatisk placering kan du slå **Opdatér sted i baggrunden** til under **Indstillinger → Sted**. Giv præcis placering og vælg **Tillad altid** i Androids appindstillinger, hvis Android kræver det. En ny præcis placering opdaterer widgetten og notifikationernes tider. Android bestemmer, hvor ofte baggrundsplacering leveres.

Vælg **Indstillinger → Tilføj 4 × 2-widget**, eller find **Salah Times 4 × 2** i Androids widgetmenu på startskærmen. Widgetten viser dagens tider, solopgang og næste bøn, følger sted og Asr-metode fra appen og åbner appen ved tryk.

Under **Indstillinger → Notifikationer** kan du slå en påmindelse 30 minutter før hver bøn og en adhan-besked ved bønnetid til hver for sig. Begge vibrerer uden lyd. Hvis Android beder om det, kan du give adgang til notifikationer og præcise alarmer. Uden adgang til præcise alarmer kan Android forsinke leveringen.

Hvis du har den tidligere smalle widget på startskærmen, skal du fjerne den og tilføje widgetten igen efter opdateringen for at få størrelsen 4 × 2.

## Byg APK’en

Brug .NET 10 med Android-arbejdsbelastningen, JDK 21 og Android SDK Platform 36.

```sh
dotnet workload install android
dotnet publish Android/SalahTimes.Android.csproj -c Release -p:AndroidPackageFormats=apk
```

APK’en findes under `Android/bin/Release/net10.0-android36.0/`. Den kræver Android 8.0 eller nyere. Brug samme signeringsnøgle og en højere versionskode til opdateringer af en installeret APK.

Hvis du vil bygge en test-APK, som kan installeres ved siden af både appen og den tidligere preview, kan du bruge et særskilt pakke-id:

```sh
dotnet publish Android/SalahTimes.Android.csproj -c Release -p:AndroidPackageFormats=apk \
  -p:ApplicationId=dk.salahtimes.android.review \
  -p:ApplicationVersion=15 -p:ApplicationDisplayVersion=1.5.4-review \
  '-p:ApplicationTitle=Salah Times Review'
```

Websitet og de delte beregningstests kan bygges som før med `dotnet test PrayerTimes.sln -c Release`.

## Byg til Google Play

Produktionspakken bruger id'et `dk.salahtimes.android`. Byg et Android App Bundle fra den revision, du vil teste.

```sh
dotnet publish Android/SalahTimes.Android.csproj -c Release -p:AndroidPackageFormats=aab
```

Signér det usignerede `dk.salahtimes.android.aab` i build-outputtet med uploadnøglen til appen i Play Console. `jarsigner` spørger efter adgangskoden.

```sh
jarsigner -keystore /sti/til/upload.jks -signedjar Salah-Times-play.aab \
  Android/bin/Release/net10.0-android36.0/dk.salahtimes.android.aab upload
jarsigner -verify Salah-Times-play.aab
```

Hvis der ligger en tidligere upload i Play Console, skal versionskoden være højere. Angiv den om nødvendigt med `-p:ApplicationVersion=<versionskode>`. Brug appens [privatlivsside](https://salahtimes.dk/privatliv#apps) i Play Console. Baggrundsplacering bruges kun efter et aktivt valg til at opdatere widgetten og påmindelserne. Google Play kan kræve en erklæring og en gennemgang af dette flow.
