#!/usr/bin/env bash
# Put a player on the studio's whitelist and print an invitation link for them.
#   sudo /opt/pgm-studio/bin/invite.sh <minecraft-name> [member|admin]
# The whitelist is an admin's, and a token never carries more than a member's rights, so a script cannot sign
# in as one. This starts a temporary studio in open mode on 127.0.0.1:7896 only, makes the two calls, and
# stops it. The public studio stays invited throughout. /admin/users does the same from an admin's browser.
# Program.cs re-adds env vars after the command line, so the overrides go in a second EnvironmentFile,
# which systemd reads after the first: the later file wins.
set -euo pipefail
PLAYER=${1:?usage: invite.sh <minecraft-name> [member|admin]}
ROLE=${2:-member}
[[ $ROLE == member || $ROLE == admin ]] || { echo "role is member or admin" >&2; exit 2; }

UNIT=pgm-studio-invite
OVR=/run/$UNIT.env
cleanup() { systemctl stop "$UNIT" 2>/dev/null || true; rm -f "$OVR"; }
trap cleanup EXIT

printf 'ASPNETCORE_URLS=http://127.0.0.1:7896\nAccess__Mode=open\n' > "$OVR"
chmod 644 "$OVR"
systemctl reset-failed "$UNIT" 2>/dev/null || true
systemctl stop "$UNIT" 2>/dev/null || true

systemd-run --quiet --unit="$UNIT" --uid=pgm-studio --gid=pgm-studio \
  -p EnvironmentFile=/etc/pgm-studio/pgm-studio.env \
  -p EnvironmentFile="$OVR" \
  -p WorkingDirectory=/opt/pgm-studio/app \
  -p RuntimeMaxSec=60 \
  -E HOME=/var/lib/pgm-studio \
  /usr/bin/dotnet /opt/pgm-studio/app/PgmStudio.Api.dll

for i in $(seq 1 30); do
  curl -sf -m 2 http://127.0.0.1:7896/api/health >/dev/null && break
  sleep 1
done
MODE=$(curl -sS http://127.0.0.1:7896/api/me | python3 -c 'import json,sys; print(json.load(sys.stdin)["mode"])')
[[ $MODE == open ]] || { echo "temporary studio answered mode '$MODE', not open; nothing done" >&2; exit 1; }

USER_JSON=$(curl -sS -X POST http://127.0.0.1:7896/api/users -H 'content-type: application/json' \
  -d "{\"player\":\"$PLAYER\",\"role\":\"$ROLE\"}")
echo "user: $USER_JSON"
UUID=$(python3 -c 'import json,sys; print(json.loads(sys.argv[1]).get("uuid") or "")' "$USER_JSON")
[[ -n $UUID ]] || { echo "no uuid in the answer; no invitation made" >&2; exit 1; }

echo -n "invitation: "
curl -sS -X POST -H 'Host: pgmstudio.de' -H 'X-Forwarded-Proto: https' \
  "http://127.0.0.1:7896/api/users/$UUID/invite"; echo
