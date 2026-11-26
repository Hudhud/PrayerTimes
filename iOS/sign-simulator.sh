#!/bin/bash
set -euo pipefail

app=${1:?Pass the path to the iPhone simulator app bundle}

# The .NET workload changes the embedded libraries' load commands during the build.
# Sign the libraries at the bundle root explicitly before sealing the app.
for library in "$app"/*.dylib; do
    if [[ -f "$library" ]]; then
        codesign --force --sign - "$library"
        codesign --verify --strict "$library"
    fi
done

codesign --force --sign - "$app"
codesign --verify --deep --strict "$app"
