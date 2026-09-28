#!/usr/bin/env bash
# Deploy a commit of pgm-studio to this server (docs/deployment.md).
#
#   sudo /opt/pgm-studio/bin/deploy.sh [git-ref]      default: origin/main
#
# Publishes the API and the migrator into releases/<sha>, dumps the database, runs --migrate-only, points
# /opt/pgm-studio/app at the release and restarts, then asks /api/health; a studio that does not answer is
# pointed back at the release before it. Last, it installs this commit's copies of tools/deploy/*.sh into
# /opt/pgm-studio/bin, so the server runs the scripts the deployed commit carries.
set -euo pipefail

REPO=${PGM_STUDIO_REPO:-/opt/pgm-studio/repo}
BASE=/opt/pgm-studio
RELEASES=$BASE/releases
BACKUPS=/var/backups/pgm-studio
STATE=/var/lib/pgm-studio-deploy
ENV_FILE=/etc/pgm-studio/pgm-studio.env
REF=${1:-origin/main}
KEEP=3
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1

# One deploy at a time, whether a person or the timer started it.
install -d -m 755 "$STATE"
exec 9>"$STATE/lock"
flock -n 9 || { echo "another deploy is running"; exit 75; }

log() { printf '\n== %s\n' "$*"; }

log "fetching $REF"
git -C "$REPO" fetch -q origin
git -C "$REPO" checkout -q --detach "$REF"
SHA=$(git -C "$REPO" rev-parse --short=12 HEAD)
REL=$RELEASES/$SHA
echo "commit $SHA: $(git -C "$REPO" log -1 --format=%s)"

fail() { echo "$SHA" > "$STATE/failed"; echo "!! $*"; exit 1; }

if [[ -d $REL/app && -d $REL/migrator ]]; then
  log "release $SHA already published, reusing it"
else
  log "publishing into $REL"
  rm -rf "$REL"
  # Nice'd: the publish shares two cores with the studio that is still serving.
  nice -n 10 dotnet publish "$REPO/src/PgmStudio.Api" -c Release -o "$REL/app" --nologo -v quiet \
    || fail "publishing the API failed"
  nice -n 10 dotnet publish "$REPO/src/PgmStudio.Import" -c Release -o "$REL/migrator" --nologo -v quiet \
    || fail "publishing the migrator failed"
  # The MSBuild nodes and the compiler server otherwise idle on ~800 MB of a box with no swap.
  dotnet build-server shutdown >/dev/null 2>&1 || true
  pkill -f 'MSBuild.dll.*nodemode' 2>/dev/null || true
fi

log "backing up the database"
install -d -m 700 "$BACKUPS"
DUMP=$BACKUPS/pgm_studio-$(date -u +%Y%m%dT%H%M%SZ)-before-$SHA.sql.gz
mariadb-dump --single-transaction --routines --triggers pgm_studio | gzip > "$DUMP"
echo "$DUMP ($(du -h "$DUMP" | cut -f1))"

# The pictures notes carry are files beside the database rather than rows in it. Each is named by its hash and
# never changes, so the copy only ever adds the ones it does not hold yet.
PICTURES=$( set -a; . "$ENV_FILE"; set +a; echo "${Notes__Pictures:-/var/lib/pgm-studio/.local/share/pgm-studio/pictures}" )
if [ -d "$PICTURES" ]; then
  install -d -m 700 "$BACKUPS/pictures"
  cp -a -n "$PICTURES/." "$BACKUPS/pictures/"
  echo "$BACKUPS/pictures ($(du -sh "$BACKUPS/pictures" | cut -f1))"
fi

log "migrating"
( set -a; . "$ENV_FILE"; set +a; cd "$REL/migrator" && dotnet PgmStudio.Import.dll --migrate-only ) \
  || fail "the migration failed; the studio still runs the release before, and $DUMP restores the schema"

log "switching and restarting"
PREV=$(readlink -f "$BASE/app" || true)
ln -sfn "$REL/app" "$BASE/app.new" && mv -T "$BASE/app.new" "$BASE/app"
systemctl restart pgm-studio

healthy() { curl -sf -m 2 http://127.0.0.1:7894/api/health >/dev/null; }
for _ in $(seq 1 30); do healthy && break; sleep 1; done
if ! healthy; then
  echo "!! health check failed; journal:"; journalctl -u pgm-studio -n 30 --no-pager
  if [[ -n $PREV && -d $PREV && $PREV != "$REL/app" ]]; then
    echo "!! rolling back to $PREV (the schema stays migrated; $DUMP restores it if needed)"
    ln -sfn "$PREV" "$BASE/app.new" && mv -T "$BASE/app.new" "$BASE/app"
    systemctl restart pgm-studio
  fi
  fail "$SHA did not come up"
fi
grep -q '{fingerprint}' "$BASE/app/wwwroot/index.html" \
  && fail "the served index.html carries an unfilled placeholder"

# The Generator page's board library, composed for this release's composer (docs/tools/generator.md). It runs
# as a unit of its own so it outlives this script, at the lowest CPU and I/O priority so the live studio always
# has the cores first; a release whose composer already has its library composes nothing and exits. A fill the
# release before started is stopped, since this release's composer is the one the library is for.
log "composing the board library in the background (journalctl -u pgm-studio-library)"
systemctl stop pgm-studio-library 2>/dev/null || true
systemctl reset-failed pgm-studio-library 2>/dev/null || true
systemd-run --unit=pgm-studio-library --uid=pgm-studio --gid=pgm-studio \
  --setenv=HOME=/var/lib/pgm-studio --property=EnvironmentFile="$ENV_FILE" --property=Nice=19 --property=CPUWeight=10 --property=IOSchedulingClass=idle \
  --working-directory="$REL/migrator" "$(command -v dotnet)" PgmStudio.Import.dll --compose-library \
  || echo "!! the board library could not be started; the studio serves the library it has"

log "pruning old releases (keeping $KEEP)"
ls -1dt "$RELEASES"/*/ 2>/dev/null | tail -n +$((KEEP + 1)) | while read -r old; do
  [[ $(readlink -f "$BASE/app") == "${old%/}/app" ]] || rm -rf "$old"
done

# Each script is written beside its old copy and renamed over it, so a script that is running (this one,
# under autodeploy) keeps reading the file it opened.
log "installing this commit's deploy scripts"
install -d -m 755 "$BASE/bin"
for script in "$REPO"/tools/deploy/*.sh; do
  install -m 750 "$script" "$BASE/bin/.$(basename "$script").new"
  mv -f "$BASE/bin/.$(basename "$script").new" "$BASE/bin/$(basename "$script")"
done

git -C "$REPO" checkout -q main && git -C "$REPO" merge -q --ff-only origin/main || true
git -C "$REPO" rev-parse "$SHA" > "$STATE/deployed"
rm -f "$STATE/failed"
log "deployed $SHA"
