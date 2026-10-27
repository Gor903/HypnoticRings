#!/usr/bin/env bash
# Regenerates the theme screenshots. Needs a graphics device, so unlike every other run here
# this one does NOT pass -nographics — see DECISIONS D-004.
set -uo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/_common.sh"

if [ -z "${DISPLAY:-}" ]; then
  if [ -e /usr/share/vulkan/icd.d/lvp_icd.json ]; then
    say "no DISPLAY; falling back to the software rasteriser"
    EXTRA=(-force-vulkan)
  else
    fail "no display and no software rasteriser — cannot render"
    exit 1
  fi
else
  EXTRA=()
fi

# Play mode, not an editor method: under a scriptable pipeline an editor-side render of a
# runtime-built scene in batch mode returns the clear colour and nothing else.
rm -f "$PROJECT_ROOT"/docs/publishing/art/screenshots/themes/*.png
run_unity screenshots "${EXTRA[@]}" -runTests -testPlatform PlayMode \
  -testFilter "HR.Tests.PlayMode.ThemeScreenshotTests.*" \
  -testResults "$PROJECT_ROOT/.artifacts/tests/screenshots.xml"
status=$?

assert_log_clean "$LOG_DIR/screenshots.log" || status=1

count=$(find "$PROJECT_ROOT/docs/publishing/art/screenshots/themes" -name '*.png' 2>/dev/null | wc -l)
if [ "$count" -eq 0 ]; then
  fail "no frames were written"
  status=1
fi

if [ "$status" -eq 0 ]; then
  ok "$count frames"
  exit 0
fi
echo "screenshots: FAILED" >&2
exit 1
