#!/usr/bin/env bash
# Install this checkout's deploy scripts and the automatic deploy onto the server (docs/deployment.md).
#
#   sudo tools/deploy/install.sh
#
# Idempotent. Clones the repository into /opt/pgm-studio/repo, the checkout every deploy works in, copies
# tools/deploy/*.sh into /opt/pgm-studio/bin, the timer and its service into
# /etc/systemd/system, and enables the timer. It records the release /opt/pgm-studio/app already points at
# as deployed, so the first tick deploys only a main that has moved past it. pgm-studio.service, the
# Caddyfile and the environment file are not touched: they are the server's, and the copies here are what
# they were set up from.
set -euo pipefail
HERE=$(cd "$(dirname "$0")" && pwd)
BASE=/opt/pgm-studio
STATE=/var/lib/pgm-studio-deploy

install -d -m 755 "$BASE/bin" "$STATE"
# The deploy's own clone, so a timer switching commits never moves a checkout somebody is working in.
if [[ ! -d $BASE/repo/.git ]]; then
  git clone -q "$(git -C "$HERE/../.." remote get-url origin)" "$BASE/repo"
  echo "cloned $BASE/repo"
fi
for script in "$HERE"/*.sh; do install -m 750 "$script" "$BASE/bin/$(basename "$script")"; done
install -m 644 "$HERE/pgm-studio-deploy.service" "$HERE/pgm-studio-deploy.timer" /etc/systemd/system/

if [[ ! -s $STATE/deployed ]] && [[ -L $BASE/app ]]; then
  release=$(basename "$(dirname "$(readlink -f "$BASE/app")")")
  if full=$(git -C "$BASE/repo" rev-parse --verify -q "$release^{commit}"); then
    echo "$full" > "$STATE/deployed"
    echo "recorded ${full:0:12} as deployed"
  fi
fi

systemctl daemon-reload
systemctl enable --now pgm-studio-deploy.timer
systemctl list-timers pgm-studio-deploy.timer --no-pager
