#!/usr/bin/env bash
# Deploy main when it has moved and its CI is green (docs/deployment.md). Run by pgm-studio-deploy.timer.
#
# Asks the remote for main's head, and does nothing when that commit is the one deployed or the one whose
# deploy last failed (a failure waits for the next merge, or for a person running deploy.sh). Otherwise asks
# GitHub for the commit's check runs, unauthenticated since the repository is public, and deploys only when
# every check this script requires has completed successfully and none has failed.
set -euo pipefail

REPO=${PGM_STUDIO_REPO:-/opt/pgm-studio/repo}
STATE=/var/lib/pgm-studio-deploy
REQUIRED_CHECKS=${PGM_STUDIO_REQUIRED_CHECKS:-"checks database-and-browser"}

HEAD=$(git -C "$REPO" ls-remote origin refs/heads/main | cut -f1)
[[ -n $HEAD ]] || { echo "could not read main from the remote"; exit 1; }
[[ $HEAD == "$(cat "$STATE/deployed" 2>/dev/null)" ]] && exit 0
[[ $HEAD == "$(cat "$STATE/failed" 2>/dev/null)" ]] && exit 0

SLUG=$(git -C "$REPO" remote get-url origin | sed -E 's#(git@github.com:|https://github.com/)##; s#\.git$##')
RUNS=$(curl -sf -m 20 -H 'Accept: application/vnd.github+json' \
  "https://api.github.com/repos/$SLUG/commits/$HEAD/check-runs?per_page=100") \
  || { echo "GitHub did not answer for $HEAD; trying again next time"; exit 0; }

VERDICT=$(python3 - "$REQUIRED_CHECKS" "$RUNS" <<'EOF'
import json, sys
required = sys.argv[1].split()
runs = json.loads(sys.argv[2]).get("check_runs", [])
latest = {}
for run in runs:
    latest.setdefault(run["name"], run)   # the API lists the newest run of a name first
failed = [n for n, r in latest.items() if r["status"] == "completed"
          and r["conclusion"] not in ("success", "skipped", "neutral")]
if failed:
    print("failed " + " ".join(failed))
elif all(n in latest and latest[n]["status"] == "completed" for n in required):
    print("green")
else:
    print("waiting " + " ".join(n for n in required
                                 if n not in latest or latest[n]["status"] != "completed"))
EOF
)

case $VERDICT in
  green)
    echo "main is ${HEAD:0:12} and green; deploying"
    exec /opt/pgm-studio/bin/deploy.sh "$HEAD" ;;
  failed*)
    echo "main is ${HEAD:0:12}, and CI failed (${VERDICT#failed }); not deploying"
    echo "$HEAD" > "$STATE/failed" ;;
  *)
    echo "main is ${HEAD:0:12}; CI is still running (${VERDICT#waiting })" ;;
esac
