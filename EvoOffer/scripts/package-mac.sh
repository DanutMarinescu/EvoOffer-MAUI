#!/bin/bash
set -euo pipefail

project_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

# Publish the Mac app project explicitly; building alone does not create an
# installer, and publishing the solution would also attempt to publish iOS.
dotnet publish "$project_root/EvoOffer/EvoOffer.csproj" \
    -f net10.0-maccatalyst27.0 -c Release -p:CreatePackage=true "$@"
