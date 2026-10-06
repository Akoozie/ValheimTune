#!/usr/bin/env bash
# Code side of the release-day loop (docs/PORTING.md): add game version $1 to the
# shipped gate, bump the plugin patch version, retarget the version tests.
# Docs (README, CHANGELOG, PORTING) are left for a person - check-docs.sh lists them.
#   ci/port-bump.sh 1.0.18
set -euo pipefail
cd "$(dirname "$0")/.."
NEW_GAME=$1
V=$(grep -oE 'Version = "[0-9.]+"' ValheimTune/Plugin.cs | grep -oE '[0-9.]+')
BUILDS=$(grep -oE 'DefaultKnownGoodBuilds = "[^"]+"' ValheimTune/Compat.cs | cut -d'"' -f2)
OLD_GAME=$(echo "$BUILDS" | tr ',' '\n' | tr -d ' ' | sort -V | tail -1)
NEW_V=$(echo "$V" | awk -F. '{ printf "%d.%d.%d", $1, $2, $3 + 1 }')
if echo "$BUILDS" | tr ',' '\n' | tr -d ' ' | grep -qx "$NEW_GAME"; then echo "$NEW_GAME is already in the gate"; exit 1; fi

sed -i "s/Version = \"$V\"/Version = \"$NEW_V\"/" ValheimTune/Plugin.cs
sed -i "s|<Version>$V</Version>|<Version>$NEW_V</Version>|" ValheimTune/ValheimTune.csproj
sed -i "s/\"version_number\": \"$V\"/\"version_number\": \"$NEW_V\"/" thunderstore/manifest.json
sed -i "s/\"$V\"/\"$NEW_V\"/" ValheimTune.Tests/SmokeTests.cs
sed -i "s/DefaultKnownGoodBuilds = \"$BUILDS\"/DefaultKnownGoodBuilds = \"$BUILDS, $NEW_GAME\"/" ValheimTune/Compat.cs
python3 ci/compat-tests-bump.py ValheimTune.Tests/CompatTests.cs "$OLD_GAME" "$NEW_GAME" "$BUILDS"

grep -qF "Version = \"$NEW_V\"" ValheimTune/Plugin.cs
grep -qF "<Version>$NEW_V</Version>" ValheimTune/ValheimTune.csproj
grep -qF "\"version_number\": \"$NEW_V\"" thunderstore/manifest.json
grep -qF "\"$NEW_V\"" ValheimTune.Tests/SmokeTests.cs
grep -qF "$BUILDS, $NEW_GAME\"" ValheimTune/Compat.cs
echo "plugin $V -> $NEW_V, gate + $NEW_GAME (previous newest $OLD_GAME)"
