#!/usr/bin/env bash
# Regenerates every captured frame. Needs a graphics device, so unlike every other run here
# this one does NOT pass -nographics — see DECISIONS D-004.
#
#   tools/screenshots.sh          everything: tunnel themes, screens, skins, contact sheets
#   tools/screenshots.sh themes   the tunnel in each theme
#   tools/screenshots.sh ui       every screen at every shape, and every theme's skin
#
# One command rather than a filter remembered by hand. The interface frames used to be
# reachable only by passing a test filter to the runner directly, which meant they were
# regenerated when somebody thought of it rather than when the interface changed.
set -uo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/_common.sh"

what="${1:-all}"
case "$what" in
  all|themes|ui) ;;
  *) echo "usage: $0 [all|themes|ui]" >&2; exit 2 ;;
esac

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

ART="$SHOTS_DIR"
status=0

# Play mode, not an editor method: under a scriptable pipeline an editor-side render of a
# runtime-built scene in batch mode returns the clear colour and nothing else.
shoot() {
  local name="$1" filter="$2" dir="$3"
  rm -f "$dir"/*.png 2>/dev/null

  run_unity "screenshots-$name" "${EXTRA[@]}" -runTests -testPlatform PlayMode \
    -testFilter "$filter" -testResults "$PROJECT_ROOT/.artifacts/tests/screenshots-$name.xml"
  local code=$?

  assert_log_clean "$LOG_DIR/screenshots-$name.log" || code=1

  local count
  count=$(find "$dir" -name '*.png' 2>/dev/null | wc -l)
  if [ "$count" -eq 0 ]; then
    fail "$name: no frames were written"
    code=1
  elif [ "$code" -eq 0 ]; then
    ok "$name: $count frames"
  fi

  return $code
}

if [ "$what" = "all" ] || [ "$what" = "themes" ]; then
  shoot themes "HR.Tests.PlayMode.ThemeScreenshotTests.*" "$ART/themes" || status=1
fi

if [ "$what" = "all" ] || [ "$what" = "ui" ]; then
  # Both captures live in one fixture and one run: the second one re-themes the project, and
  # splitting them across runs would mean paying the engine's cold start twice to save nothing.
  shoot ui "HR.Tests.PlayMode.UiScreenshotTests.*" "$ART/screens" || status=1
  count=$(find "$ART/skins" -name '*.png' 2>/dev/null | wc -l)
  [ "$count" -gt 0 ] && ok "skins: $count frames" || { fail "no skin frames were written"; status=1; }
fi

# Sheets last and only on success: a contact sheet built from a half-written set is a picture
# of a failure that looks like a picture of the game.
if [ "$status" -eq 0 ]; then
  python3 "$PROJECT_ROOT/tools/contact_sheet.py" || status=1
fi

echo
if [ "$status" -eq 0 ]; then ok "screenshots: OK"; exit 0; fi
echo "screenshots: FAILED" >&2
exit 1
