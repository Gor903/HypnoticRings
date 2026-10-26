#!/usr/bin/env bash
# Applies project settings and regenerates the scenes. Idempotent — safe to re-run.
set -uo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/_common.sh"

run_unity bootstrap -nographics -quit -executeMethod HR.Editor.CI.CIBootstrap
status=$?

# A batch-mode editor exits 0 when compilation fails before the method is ever reached, so
# the exit code alone proves nothing. The log and the marker line decide.
assert_log_clean "$LOG_DIR/bootstrap.log" || status=1
if ! grep -q "CI step OK" "$LOG_DIR/bootstrap.log"; then
  fail "CIBootstrap never reported success — the method may not have run at all"
  status=1
fi

if [ "$status" -eq 0 ]; then echo "bootstrap: OK"; else echo "bootstrap: FAILED" >&2; fi
exit "$status"
