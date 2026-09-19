#!/usr/bin/env bash
# Doc staleness check. Truth comes from code: the plugin version from Plugin.cs,
# the game builds from Compat.DefaultKnownGoodBuilds. Every doc that restates
# them must agree. Run from anywhere; exits 1 on any stale line.
#   --steam  also fail if Steam news names a Valheim patch newer than the newest known build
set -u
cd "$(dirname "$0")"
fail=0
bad() { echo "STALE: $*"; fail=1; }
has() { tr -d '\r' < "$1" | grep -qF -- "$2" || bad "$1 does not say: $2"; }

V=$(grep -oE 'Version = "[0-9.]+"' ValheimTune/Plugin.cs | grep -oE '[0-9.]+')
BUILDS=$(grep -oE 'DefaultKnownGoodBuilds = "[^"]+"' ValheimTune/Compat.cs | cut -d'"' -f2)
NEWEST=$(echo "$BUILDS" | tr ',' '\n' | tr -d ' ' | sort -V | tail -1)
[ -n "$V" ] && [ -n "$NEWEST" ] || { echo "could not read version/builds from code"; exit 2; }
echo "plugin $V, known builds: $BUILDS (newest $NEWEST)"

has ValheimTune/ValheimTune.csproj "<Version>$V</Version>"
has thunderstore/manifest.json "\"version_number\": \"$V\""
has ValheimTune.Tests/SmokeTests.cs "\"$V\""
has ValheimTune.Tests/CompatTests.cs "IsKnown(\"$NEWEST\", Compat.DefaultKnownGoodBuilds)"

has README.md "Valheim $NEWEST ready"
has README.md "**$V runs on game $NEWEST"
has README.md "(currently \`$BUILDS\`)"
has README.md "| $BUILDS | patch-time"
has README.md "$V loaded on game $NEWEST"
# the "unknown build" example must name a build that really is unknown
EX=$(tr -d '\r' < README.md | grep -oE 'game [0-9.]+ not in KnownGoodBuilds' | grep -oE '[0-9]+\.[0-9]+\.[0-9]+')
for b in $EX; do echo "$BUILDS" | tr ',' '\n' | tr -d ' ' | grep -qx "$b" && bad "README.md unknown-build example $b is a known build"; done

if [ -f CHANGELOG.md ]; then
  TOP=$(grep -m1 -oE '^## [0-9.]+' CHANGELOG.md | cut -c4-)
  [ "$TOP" = "$V" ] || bad "CHANGELOG.md top entry is $TOP, plugin is $V"
  has CHANGELOG.md "$V targets"
fi
[ -f docs/PORTING.md ] && has docs/PORTING.md "# What actually happened: $NEWEST"

if [ "${1:-}" = "--steam" ]; then
  # ponytail: parses "Patch 1.0.N" / "Hotfix 1.0.N" out of news titles; breaks if Iron Gate renames them
  LATEST=$(curl -fsS "https://api.steampowered.com/ISteamNews/GetNewsForApp/v2/?appid=892970&count=20&maxlength=1" \
    | grep -oE '"title":"[^"]*' | grep -iE 'patch|hotfix' | grep -oE '[0-9]+\.[0-9]+\.[0-9]+' | sort -V | tail -1)
  echo "newest patch in Steam news: ${LATEST:-none found}"
  if [ -n "$LATEST" ] && [ "$(printf '%s\n%s\n' "$NEWEST" "$LATEST" | sort -V | tail -1)" != "$NEWEST" ]; then
    bad "Valheim $LATEST is out, KnownGoodBuilds stops at $NEWEST - run the port loop in docs/PORTING"
  fi
fi

[ $fail = 0 ] && echo "docs OK"
exit $fail
