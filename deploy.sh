#!/bin/sh
# Build, ship to the homelab server, restart it, show the plugin's log lines.
# Restarting takes the world through a full save (150 s grace). Warn players first.
# Needs Tailscale SSH approved for this desktop (browser check on first use).
set -e
HOST="${HOST:?set HOST=user@your-server}"
# Container name and plugin path assume the lloesche/valheim-server image; adapt the docker lines otherwise.
DLL=ValheimTune/bin/Release/netstandard2.1/ValheimTune.dll
cd "$(dirname "$0")"
"${DOTNET:-$HOME/.dotnet/dotnet.exe}" build ValheimTune/ValheimTune.csproj -c Release
SHA=$(sha256sum "$DLL" | cut -c1-12)
# config/bepinex/plugins is root-owned on the host; scp to /tmp with a unique name, then docker cp.
scp "$DLL" "$HOST:/tmp/ValheimTune-$SHA.dll"
ssh "$HOST" "docker cp /tmp/ValheimTune-$SHA.dll valheim:/config/bepinex/plugins/ValheimTune.dll && rm /tmp/ValheimTune-$SHA.dll && docker exec valheim sha256sum /config/bepinex/plugins/ValheimTune.dll && docker restart -t 150 valheim"
echo "restarted ($SHA); waiting 100 s for world load"
sleep 100
ssh "$HOST" 'docker logs --since 3m valheim 2>&1 | grep -i -E "ValheimTune|BepInEx.*(error|exception)" | tail -20' || echo "no ValheimTune lines found in the last 3 minutes"
