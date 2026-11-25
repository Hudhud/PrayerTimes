"""Build the worldwide city index from the GeoNames cities5000 ZIP."""
import argparse
import io
import json
import zipfile
from pathlib import Path

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("source", type=Path)
parser.add_argument("countries", type=Path)
parser.add_argument("regions", type=Path)
parser.add_argument("output", type=Path)
args = parser.parse_args()

countries = {}
for line in args.countries.read_text(encoding="utf-8").splitlines():
    if line and not line.startswith("#"):
        fields = line.split("\t")
        countries[fields[0]] = fields[4]

regions = {}
for line in args.regions.read_text(encoding="utf-8").splitlines():
    code, name, _, _ = line.split("\t")
    regions[code] = name

cities = []
with zipfile.ZipFile(args.source) as source, source.open("cities5000.txt") as table:
    for line in io.TextIOWrapper(table, encoding="utf-8"):
        row = line.rstrip("\n").split("\t")
        identifier, name, ascii_name, _, lat, lon, _, _, country = row[:9]
        if country == "DK" or not row[17]:
            continue
        region = regions.get(f"{country}.{row[10]}")
        country_name = countries.get(country, country)
        area = f"{region}, {country_name}" if region and region != name else country_name
        city = {
            "id": f"city-{identifier}",
            "name": name,
            "area": area,
            "latitude": round(float(lat), 5),
            "longitude": round(float(lon), 5),
            "timeZoneId": row[17],
            "countryCode": country,
            "population": int(row[14]),
        }
        if ascii_name and ascii_name != name:
            city["alternateName"] = ascii_name
        cities.append(city)

cities.sort(key=lambda city: int(city["id"][5:]))
args.output.write_text("[\n" + ",\n".join(
    json.dumps(city, ensure_ascii=False, separators=(",", ":")) for city in cities
) + "\n]\n", encoding="utf-8")
print(f"{len(cities):,} cities")
