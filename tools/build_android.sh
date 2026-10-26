#!/usr/bin/env bash
# Android build. Phase 0 ships the dev APK path only; the signed .aab arrives with
# ReleaseTool in phase 10 and this script grows a 'release' mode then.
#
#   tools/build_android.sh dev        development APK, proves the toolchain
set -uo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/_common.sh"

mode="${1:-dev}"

"$PROJECT_ROOT/tools/preflight.sh" >/dev/null || { fail "preflight failed; not building"; exit 1; }

case "$mode" in
  dev)
    run_unity build-dev -nographics -executeMethod HR.Editor.CI.CIBuildAndroidDev
    status=$?
    assert_log_clean "$LOG_DIR/build-dev.log" || status=1
    artifact="$PROJECT_ROOT/Build/hypnoticrings-dev.apk"
    if [ "$status" -eq 0 ] && [ -f "$artifact" ]; then
      ok "built $(basename "$artifact") ($(du -h "$artifact" | cut -f1))"
      exit 0
    fi
    fail "no artifact produced; see ${LOG_DIR#$PROJECT_ROOT/}/build-dev.log"
    exit 1
    ;;
  release)
    fail "release mode arrives in phase 10 with ReleaseTool"
    exit 2
    ;;
  *)
    echo "usage: $0 [dev|release]" >&2
    exit 2
    ;;
esac
