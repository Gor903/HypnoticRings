#!/usr/bin/env bash
# Environment gate. Everything a long Unity run assumes, checked before it starts —
# each of these has cost someone an afternoon somewhere.
set -uo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/_common.sh"

problems=0
check() { if eval "$2" >/dev/null 2>&1; then ok "$1"; else fail "$1"; problems=$((problems + 1)); fi; }

echo "Toolchain"
check "Unity $UNITY_VERSION at $UNITY_BIN"      "[ -x '$UNITY_BIN' ]"
check "Android player module"                    "[ -d '$ANDROID_PLAYER' ]"
check "bundled OpenJDK"                          "[ -x '$JAVA_BIN' ] && [ -x '$KEYTOOL_BIN' ]"
check "Android NDK"                              "[ -d '$ANDROID_PLAYER/NDK' ]"
check "bundletool"                               "ls '$ANDROID_PLAYER'/Tools/bundletool-all-*.jar"
check "build-tools"                              "ls -d '$ANDROID_PLAYER'/SDK/build-tools/*/"
check "git"                                      "git --version"
check "python3"                                  "python3 --version"
check "Pillow (store art generation)"            "python3 -c 'import PIL'"

# The SDK platform for the target API is the one thing Unity does not guarantee, and it fails
# only after the full IL2CPP compile — which costs the entire run.
echo
echo "Android SDK"
target_api=$(grep -oP 'AndroidApiLevel\K\d+|targetSdkVersion = \(AndroidSdkVersions\)\K\d+' \
             "$PROJECT_ROOT/Assets/_Project/Editor/ProjectSetup.cs" 2>/dev/null | tail -1)
target_api="${target_api:-36}"
check "platform android-$target_api installed"   "[ -d '$ANDROID_PLAYER/SDK/platforms/android-$target_api' ]"

echo
echo "Machine"
avail=$(free -g | awk '/^Mem:/ {print $7}')
disk=$(df -BG --output=avail "$PROJECT_ROOT" | tail -1 | tr -dc '0-9')
check "RAM available ${avail}GiB (want >= 6)"    "[ '$avail' -ge 6 ]"
check "disk free ${disk}GB (want >= 20)"         "[ '$disk' -ge 20 ]"
check "cgroup memory controller delegated" \
  "grep -q memory /sys/fs/cgroup/user.slice/user-\$(id -u).slice/cgroup.subtree_control"

echo
echo "Project"
check "no other Unity editor running"            "assert_no_other_unity"
check "no stale project lock"                    "[ ! -f '$PROJECT_ROOT/Temp/UnityLockfile' ] || assert_no_other_unity"
check "render output available (display or lavapipe)" \
  "[ -n \"\${DISPLAY:-}\" ] || ls /usr/share/vulkan/icd.d/lvp_icd.json"

echo
if [ "$problems" -eq 0 ]; then
  echo "preflight: OK"
  exit 0
fi
echo "preflight: $problems problem(s)" >&2
exit 1
