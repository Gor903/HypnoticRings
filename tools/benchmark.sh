#!/usr/bin/env bash
# Phase 1 measurements. Both write markdown into .artifacts/, which is where the numbers
# quoted in docs/DECISIONS.md come from — so a decision can be rechecked rather than recalled.
#
#   tools/benchmark.sh              both
#   tools/benchmark.sh compression  ring compression strategies
#   tools/benchmark.sh rate         fixed physics step
set -uo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/_common.sh"

which="${1:-both}"
failed=0

run_one() {
  local name="$1" method="$2" report="$3"
  rm -f "$PROJECT_ROOT/$report"
  run_unity "bench-$name" -nographics -executeMethod "$method"
  local status=$?

  assert_log_clean "$LOG_DIR/bench-$name.log" || status=1
  if [ ! -f "$PROJECT_ROOT/$report" ]; then
    fail "$name produced no report at $report"
    status=1
  fi

  if [ "$status" -eq 0 ]; then
    ok "$name"
    echo
    cat "$PROJECT_ROOT/$report"
    echo
  else
    failed=1
  fi
}

case "$which" in
  compression) run_one compression HR.Editor.CompressionBenchmark.CIRun docs/benchmarks/compression-benchmark.md ;;
  rate)        run_one rate HR.Editor.PhysicsRateBenchmark.CIRun docs/benchmarks/physics-rate-benchmark.md ;;
  both)
    run_one compression HR.Editor.CompressionBenchmark.CIRun docs/benchmarks/compression-benchmark.md
    run_one rate HR.Editor.PhysicsRateBenchmark.CIRun docs/benchmarks/physics-rate-benchmark.md
    ;;
  *) echo "usage: $0 [compression|rate|both]" >&2; exit 2 ;;
esac

[ "$failed" -eq 0 ] && { echo "benchmarks: OK"; exit 0; }
echo "benchmarks: FAILED" >&2
exit 1
