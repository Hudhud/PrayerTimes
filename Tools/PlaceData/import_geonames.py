"""Build Salah Times' local place index from the GeoNames Denmark ZIP files."""

import argparse
import json
import zipfile
from pathlib import Path

# Existing routes keep their established names and coordinates.
CITIES = {
    "2618425": ("koebenhavn", "København", 55.6759142, 12.5691285),
    "2615876": ("odense", "Odense", 55.4037560, 10.4023700),
    "2624652": ("aarhus", "Aarhus", 56.1629390, 10.2039210),
    "2624886": ("aalborg", "Aalborg", 57.0488195, 9.9217470),
}
POPULATED = {"PPL", "PPLL", "PPLA", "PPLA2", "PPLA3", "PPLA4", "PPLC"}


def rows(archive):
    with zipfile.ZipFile(archive) as source:
        return [line.split("\t") for line in source.read("DK.txt").decode("utf-8").splitlines()]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("places", type=Path)
    parser.add_argument("postal", type=Path)
    parser.add_argument("output", type=Path)
    args = parser.parse_args()

    source = rows(args.places)
    municipalities = {
        (row[10], row[11]): row[1].removesuffix(" Municipality")
        for row in source
        if row[7] == "ADM2"
    }
    result = {}
    for row in source:
        geoname_id, name, _ascii, _aliases, latitude, longitude, _class, code = row[:8]
        if code not in POPULATED and geoname_id not in CITIES:
            continue
        place_id, name, lat, lon = CITIES.get(
            geoname_id, (f"sted-{geoname_id}", name, round(float(latitude), 5), round(float(longitude), 5))
        )
        result[place_id] = {
            "id": place_id,
            "name": name,
            "area": municipalities.get((row[10], row[11]), ""),
            "latitude": lat,
            "longitude": lon,
        }

    missing = {city[0] for city in CITIES.values()} - result.keys()
    if missing:
        raise ValueError(f"Missing established cities: {sorted(missing)}")

    for row in rows(args.postal):
        country, code, name, _region, _region_code, municipality = row[:6]
        if country != "DK" or len(code) != 4 or not code.isascii() or not code.isdigit():
            continue
        place_id = f"post-{code}"
        # Each Danish code gets one representative point from the postal file.
        result.setdefault(place_id, {
            "id": place_id,
            "name": name,
            "area": municipality,
            "latitude": round(float(row[9]), 5),
            "longitude": round(float(row[10]), 5),
            "postalCode": code,
        })

    args.output.write_text(json.dumps(list(result.values()), ensure_ascii=False, separators=(",", ":")) + "\n")
    print(f"{sum('postalCode' not in p for p in result.values())} localities, "
          f"{sum('postalCode' in p for p in result.values())} postal codes")


if __name__ == "__main__":
    main()
