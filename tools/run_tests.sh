#!/usr/bin/env bash
# EditMode and PlayMode from the command line. Green tests are a precondition of the release
# build, not a separate ritual.
#
#   tools/run_tests.sh            both suites
#   tools/run_tests.sh edit       EditMode only
#   tools/run_tests.sh play       PlayMode only
set -uo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/_common.sh"

which="${1:-both}"
results_dir="$PROJECT_ROOT/.artifacts/tests"
mkdir -p "$results_dir"
failed=0

# The runner's XML is the answer; the log is tens of thousands of lines of everything else.
summarise() {
  local platform="$1" xml="$2"
  if [ ! -f "$xml" ]; then
    fail "$platform: no results file — the run did not reach the test runner"
    return 1
  fi
  python3 - "$platform" "$xml" <<'PY'
import sys, xml.etree.ElementTree as ET
platform, path = sys.argv[1], sys.argv[2]
root = ET.parse(path).getroot()
total = int(root.get('total', 0)); passed = int(root.get('passed', 0))
failed = int(root.get('failed', 0)); skipped = int(root.get('skipped', 0))
print(f"  {platform}: {passed}/{total} passed, {failed} failed, {skipped} skipped")
for case in root.iter('test-case'):
    if case.get('result') == 'Failed':
        msg = (case.findtext('failure/message') or '').strip().splitlines()
        print(f"    FAILED {case.get('fullname')}: {msg[0] if msg else ''}")
sys.exit(1 if failed or total == 0 else 0)
PY
}

run_suite() {
  local platform="$1" lower
  lower=$(echo "$platform" | tr '[:upper:]' '[:lower:]')
  local xml="$results_dir/$lower.xml"
  rm -f "$xml"

  # -nographics is safe here: tests do not need a rendering device, and asking for one on a
  # headless run is how a suite hangs instead of failing.
  run_unity "tests-$lower" -nographics -runTests -testPlatform "$platform" \
            -testResults "$xml" -batchmode || true

  summarise "$platform" "$xml" || failed=1
}

case "$which" in
  edit) run_suite EditMode ;;
  play) run_suite PlayMode ;;
  both) run_suite EditMode; run_suite PlayMode ;;
  *) echo "usage: $0 [edit|play|both]" >&2; exit 2 ;;
esac

echo
if [ "$failed" -eq 0 ]; then echo "tests: OK"; exit 0; fi
echo "tests: FAILED" >&2
exit 1
