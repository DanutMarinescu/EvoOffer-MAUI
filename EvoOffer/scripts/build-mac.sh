#!/bin/bash
set -euo pipefail

project_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

# Xcode selection and local output paths are configured in the project so IDE
# builds and publishing use the same settings.
dotnet build "$project_root/EvoOffer/EvoOffer.csproj" \
    -f net10.0-maccatalyst27.0 "$@"
