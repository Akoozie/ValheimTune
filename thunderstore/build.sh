#!/bin/sh
# Assemble the Thunderstore package zip. Run from the repo root:  ./mod/thunderstore/build.sh
# Thunderstore rules this satisfies: icon.png exactly 256x256, manifest.json at the zip root,
# README.md at the zip root, and the plugin under plugins/ so mod managers place it in
# BepInEx/plugins/. Publishing is manual - upload the zip at thunderstore.io.
set -e
cd "$(dirname "$0")/.."
TS=thunderstore
DLL=ValheimTune/bin/Release/netstandard2.1/ValheimTune.dll
VERSION=$(python -c "import json;print(json.load(open('$TS/manifest.json'))['version_number'])")

[ -f "$DLL" ] || { echo "no Release build at $DLL - run: dotnet build ValheimTune/ValheimTune.csproj -c Release"; exit 1; }

# manifest version must match the plugin version, or Thunderstore ships a lie
PLUGIN=$(grep -oE 'public const string Version = "[0-9.]+"' ValheimTune/Plugin.cs | grep -oE '[0-9.]+')
[ "$VERSION" = "$PLUGIN" ] || { echo "version mismatch: manifest $VERSION, Plugin.cs $PLUGIN"; exit 1; }

OUT="$TS/build"
rm -rf "$OUT" && mkdir -p "$OUT/plugins"
cp "$TS/manifest.json" "$TS/icon.png" "$OUT/"
cp README.md "$OUT/README.md"
cp "$DLL" "$OUT/plugins/ValheimTune.dll"

ZIP="$TS/ValheimTune-$VERSION.zip"
rm -f "$ZIP"
( cd "$OUT" && python -c "
import zipfile, os, sys
z = zipfile.ZipFile(os.path.join('..', 'ValheimTune-$VERSION.zip'), 'w', zipfile.ZIP_DEFLATED)
for root, _, files in os.walk('.'):
    for f in files:
        p = os.path.join(root, f)
        z.write(p, os.path.relpath(p, '.').replace(os.sep, '/'))
z.close()
" )
rm -rf "$OUT"
echo "built $ZIP"
python -c "
import zipfile
z = zipfile.ZipFile('$ZIP')
for i in z.infolist(): print(f'  {i.file_size:>8,}  {i.filename}')
"
