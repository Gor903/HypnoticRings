#!/usr/bin/env bash
# The difficulty sweep and its report. Simulation runs in the game's own code; the charts
# are rendered separately so plotting can change without re-running the simulation.
set -uo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/_common.sh"

run_unity balance -nographics -executeMethod HR.Editor.BalanceReport.CIRun
status=$?

assert_log_clean "$LOG_DIR/balance.log" || status=1
if [ ! -f "$PROJECT_ROOT/docs/benchmarks/balance-raw.csv" ]; then
  fail "the sweep produced no results"
  status=1
fi

if [ "$status" -ne 0 ]; then echo "balance: FAILED" >&2; exit 1; fi

grep -ohE "\[HR\] balance [a-z]+:[^\"]*" "$LOG_DIR/balance.log" | sort -u

python3 "$PROJECT_ROOT/tools/plot_balance.py" || { fail "plotting failed"; exit 1; }
echo "balance: OK"
