#!/usr/bin/env bash
# Shared paths and the guarded Unity runner. Sourced by every other script in tools/.
#
# Deliberately no `set -e`: every caller needs to capture a non-zero status from a Unity run
# and then read the log, and errexit would abort them one line before they could.
set -uo pipefail

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
LOG_DIR="$PROJECT_ROOT/.logs"
mkdir -p "$LOG_DIR"

UNITY_VERSION="${UNITY_VERSION:-6000.6.4f1}"
UNITY_ROOT="${UNITY_ROOT:-$HOME/Unity/Hub/Editor/$UNITY_VERSION/Editor}"
UNITY_BIN="${UNITY_PATH:-$UNITY_ROOT/Unity}"

ANDROID_PLAYER="$UNITY_ROOT/Data/PlaybackEngines/AndroidPlayer"
JAVA_BIN="$ANDROID_PLAYER/OpenJDK/bin/java"
KEYTOOL_BIN="$ANDROID_PLAYER/OpenJDK/bin/keytool"

# Budget: leave the desktop ~3 GiB and a couple of cores. Unity is not the only thing running.
MEM_HIGH="${MEM_HIGH:-8G}"
MEM_MAX="${MEM_MAX:-10G}"
CPU_QUOTA="${CPU_QUOTA:-600%}"

say()  { printf '  %s\n' "$*"; }
ok()   { printf '  ok    %s\n' "$*"; }
fail() { printf '  FAIL  %s\n' "$*" >&2; }

# Lists real editor processes. Matching on the command line is not good enough: a watcher
# shell whose own arguments mention the editor matches itself, and so does the grep.
# Identity comes from /proc/<pid>/exe, which cannot be spoofed by an argument string.
# Two traps, both hit here. Matching the command line alone is too loose: a watcher shell
# whose own arguments mention the editor matches itself. Matching the process name is too
# tight: the editor's comm is "Unity Main Thre", truncated at 15 characters, so -x Unity
# never matches anything and the guard silently never fires.
# So: cast wide with -f, then settle identity with /proc/<pid>/exe, which no argument can fake.
running_unity_pids() {
  local pid exe
  for pid in $(pgrep -f "Editor/Unity" 2>/dev/null); do
    [ "$pid" = "$$" ] && continue
    exe=$(readlink -f "/proc/$pid/exe" 2>/dev/null) || continue
    case "$exe" in */Editor/Unity) echo "$pid" ;; esac
  done
}

# Refuses to start a second editor. Two batch-mode runs on different projects start happily
# and together they do not fit in this machine's RAM.
assert_no_other_unity() {
  local pids
  pids=$(running_unity_pids)
  [ -z "$pids" ] && return 0
  fail "another Unity editor is running; one at a time, machine-wide"
  ps -o pid=,args= -p $(echo "$pids" | tr '\n' ',' | sed 's/,$//') >&2
  return 1
}

# run_unity <log-name> <args...>
# Detached, memory-capped, logging to a file. Never -logFile /dev/stdout: a play-mode run is
# a quarter of a million lines and none of them are the answer.
run_unity() {
  local log_name="$1"; shift
  local log_file="$LOG_DIR/$log_name.log"
  : > "$log_file"

  assert_no_other_unity || return 1

  # A killed editor leaves its lock behind, and the next run then refuses to start for a
  # reason that has nothing to do with what is being run. Safe to clear: nothing is running.
  if [ -f "$PROJECT_ROOT/Temp/UnityLockfile" ]; then
    say "clearing a stale project lock left by a killed editor"
    rm -f "$PROJECT_ROOT/Temp/UnityLockfile"
  fi

  local scope=(systemd-run --user --scope -q
               -p "MemoryHigh=$MEM_HIGH" -p "MemoryMax=$MEM_MAX" -p "CPUQuota=$CPU_QUOTA"
               nice -n 10)
  if ! systemd-run --user --scope -q true 2>/dev/null; then
    say "cgroup budget unavailable; running unbudgeted and watching instead"
    scope=(nice -n 10)
  fi

  say "unity: $log_name (log: ${log_file#$PROJECT_ROOT/})"
  "${scope[@]}" "$UNITY_BIN" -batchmode -projectPath "$PROJECT_ROOT" \
      -logFile "$log_file" "$@" &
  local pid=$!

  watch_run "$pid" "$log_file"
  wait "$pid"
}

# Samples while the run proceeds and aborts on the resource rules rather than asking.
#
# STALL_LIMIT exists because a hung editor looks exactly like a working one from the outside:
# the process is alive, memory is fine, swap is empty, and nothing is happening. A run once sat
# like that for five hours with its log untouched the whole time. Memory is not progress.
STALL_LIMIT="${STALL_LIMIT:-600}"     # seconds with no new log output before giving up
RUN_LIMIT="${RUN_LIMIT:-5400}"        # hard ceiling on any single engine run

watch_run() {
  local pid="$1" log_file="$2" rising=0 prev_swap=0 ticks=0
  local last_size=-1 stalled=0 elapsed=0

  while kill -0 "$pid" 2>/dev/null; do
    sleep 20
    ticks=$((ticks + 1))
    elapsed=$((elapsed + 20))

    local avail swap disk size
    avail=$(free -m | awk '/^Mem:/ {print $7}')
    swap=$(free -m | awk '/^Swap:/ {print $3}')
    disk=$(df -BG --output=avail "$PROJECT_ROOT" | tail -1 | tr -dc '0-9')
    size=$(stat -c %s "$log_file" 2>/dev/null || echo 0)

    if [ "$size" -eq "$last_size" ]; then stalled=$((stalled + 20)); else stalled=0; fi
    last_size="$size"

    if [ "$swap" -gt "$prev_swap" ]; then rising=$((rising + 1)); else rising=0; fi
    prev_swap="$swap"

    if [ "$stalled" -ge "$STALL_LIMIT" ]; then
      fail "no log output for ${stalled}s — the editor is hung, not working. Aborting."
      kill "$pid" 2>/dev/null; sleep 5; kill -9 "$pid" 2>/dev/null || true
      return 1
    fi

    if [ "$elapsed" -ge "$RUN_LIMIT" ]; then
      fail "run exceeded ${RUN_LIMIT}s — aborting rather than letting it sit"
      kill "$pid" 2>/dev/null; sleep 5; kill -9 "$pid" 2>/dev/null || true
      return 1
    fi

    # Rising swap on its own is not thrashing. This machine runs a browser and a chat
    # client that between them sit on two gigabytes of swap before anything starts, so any
    # build that touches swap at all looked like an emergency — the IL2CPP conversion was
    # killed with 4.3 GB still free and 188 GB of disk. Thrashing is swap climbing *while*
    # memory is nearly gone, which is what the pair of conditions says now. The hard floor
    # is unchanged: below a gigabyte available, stop regardless.
    if [ "$avail" -lt 1024 ] || { [ "$rising" -ge 3 ] && [ "$avail" -lt 2048 ]; } || [ "$disk" -lt 5 ]; then
      fail "resource rule hit (avail ${avail}M, swap ${swap}M, disk ${disk}G) — aborting"
      kill "$pid" 2>/dev/null || true
      return 1
    fi

    # Every two minutes, so a long run leaves a trace of how it behaved.
    if [ $((ticks % 6)) -eq 0 ]; then
      say "  ... ${elapsed}s, avail ${avail}M, swap ${swap}M, $(wc -l < "$log_file" 2>/dev/null) log lines"
    fi
  done
}

# A batch-mode editor can exit 0 after logging a build failure, so the log gets read too.
assert_log_clean() {
  local log_file="$1"
  if grep -nE "error CS|BuildFailedException|Error building|Build Failed|Licence|License is not" \
       "$log_file" | grep -v "0 error" | head -20; then
    fail "errors found in ${log_file#$PROJECT_ROOT/}"
    return 1
  fi
  return 0
}
