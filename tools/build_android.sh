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
    # Credentials come from the environment and never from the project. The env file lives
    # beside the keystore at mode 600; sourcing it here means the passwords exist only inside
    # this process, and ReleaseTool wipes them out of ProjectSettings in a finally block even
    # when the build throws.
    env_file="$HOME/keystores/hypnoticrings-signing.env"
    if [ -f "$env_file" ]; then
      set -a; . "$env_file"; set +a
    fi

    missing=""
    for v in HYPNOTICRINGS_KEYSTORE HYPNOTICRINGS_KEYSTORE_PASS \
             HYPNOTICRINGS_KEY_ALIAS HYPNOTICRINGS_KEY_PASS; do
      [ -z "${!v:-}" ] && missing="$missing $v"
    done
    if [ -n "$missing" ]; then
      fail "unsigned build refused; missing:$missing"
      echo "  expected $env_file (mode 600), see _shared/KEYS.md" >&2
      exit 1
    fi

    run_unity build-release -nographics -executeMethod HR.Editor.ReleaseTool.CIBuild
    status=$?
    assert_log_clean "$LOG_DIR/build-release.log" || status=1

    artifact="$PROJECT_ROOT/Build/hypnoticrings.aab"
    if [ "$status" -eq 0 ] && [ -f "$artifact" ]; then
      ok "built $(basename "$artifact") ($(du -h "$artifact" | cut -f1))"
      echo "  verify it with tools/verify_artifact.sh before trusting any of it" >&2
      exit 0
    fi
    fail "no bundle produced; see ${LOG_DIR#$PROJECT_ROOT/}/build-release.log"
    exit 1
    ;;
  *)
    echo "usage: $0 [dev|release]" >&2
    exit 2
    ;;
esac
