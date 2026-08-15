#!/usr/bin/env bash
# Builds every file-based tool script — the `.cs` files under tools/ that carry a `#:project` directive and
# are therefore NOT in PgmStudio.slnx. `dotnet build` at the repo root never touches them, so a rename in
# src/ can leave a script uncompilable for months while the solution stays green: that is how 35 of them
# broke at once (B227), including the showcase model.md is meant to be read against.
#
# Usage: tools/build-scripts.sh [name-fragment]
#   with no argument, every script; with one, only those whose path contains it.
set -uo pipefail
cd "$(dirname "$0")/.."

filter="${1:-}"
mapfile -t scripts < <(grep -rl '^#:project' --include='*.cs' tools/ \
    | grep -v '/obj/' | grep -v '/bin/' | { [ -n "$filter" ] && grep -- "$filter" || cat; } | sort)

if [ "${#scripts[@]}" -eq 0 ]; then
    echo "no file-based scripts matched${filter:+ '$filter'}" >&2
    exit 1
fi

failed=()
for script in "${scripts[@]}"; do
    # Retried once because a build here is not always deterministic: the shared folder and the per-script
    # runfile cache between them produce the odd "Unable to find fallback package folder" on a first build.
    # A real compile error fails both attempts, so this hides nothing — it only stops the gate crying wolf.
    if output=$(dotnet build "$script" -v q --nologo 2>&1) || output=$(dotnet build "$script" -v q --nologo 2>&1)
    then
        printf '  ok   %s\n' "$script"
    else
        printf '  FAIL %s\n' "$script"
        printf '%s\n' "$output" | grep -E ': (error|warning MSB)' | head -5 | sed 's/^/         /'
        failed+=("$script")
    fi
done

printf '\n%d of %d built\n' "$((${#scripts[@]} - ${#failed[@]}))" "${#scripts[@]}"
[ "${#failed[@]}" -eq 0 ] || { printf 'failed: %s\n' "${failed[*]}"; exit 1; }
