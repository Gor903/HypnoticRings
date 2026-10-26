#!/usr/bin/env bash
# Static hygiene. The release branch carries no scaffolding and no secrets — checked by
# command, because "I removed those" is exactly the claim that turns out to be false.
set -uo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/_common.sh"

problems=0
all_sources=$(find "$PROJECT_ROOT/Assets" -name '*.cs' 2>/dev/null)

# Code that actually ships. HR.Editor is Editor-only in its asmdef and HR.Tests.* is gated on
# UNITY_INCLUDE_TESTS, so neither reaches a player — and the CI marker lines in HR.Editor are
# the only way a batch-mode run reports anything, which is load-bearing for tools/bootstrap.sh.
runtime_sources=$(echo "$all_sources" | grep -v -e '/Editor/' -e '/Tests/' || true)

scan() {
  local label="$1" pattern="$2" files="$3"
  local hits
  [ -z "$files" ] && { ok "$label (no files in scope)"; return; }
  hits=$(grep -nE "$pattern" $files 2>/dev/null || true)
  if [ -n "$hits" ]; then
    fail "$label"
    echo "$hits" | sed "s|$PROJECT_ROOT/||" | head -20
    problems=$((problems + 1))
  else
    ok "$label"
  fi
}

echo "Source hygiene"
scan "no TODO / FIXME / HACK (everywhere)" \
     '(^|[^A-Za-z])(TODO|FIXME|HACK|XXX)([^A-Za-z]|$)' "$all_sources"
scan "no debug logging in shipping code" \
     'Debug\.Log(Warning|Error)?\s*\(' "$runtime_sources"
scan "no commented-out code" \
     '^[[:space:]]*//[[:space:]]*(if|for|while|return|var|public|private|void)[[:space:]]' "$all_sources"

# The exclusion above is only honest while HR.Editor really is editor-only.
if ! grep -q '"Editor"' "$PROJECT_ROOT/Assets/_Project/Editor/HR.Editor.asmdef" 2>/dev/null; then
  fail "HR.Editor is no longer Editor-only — its logging would ship"
  problems=$((problems + 1))
else
  ok "HR.Editor is Editor-only, so its logging cannot ship"
fi

echo
echo "Secrets"
scan "no hardcoded keystore passwords" \
     '(keystorePass|keyaliasPass|storePassword|keyPassword)[[:space:]]*=[[:space:]]*"[^"]+"' "$all_sources"
if git -C "$PROJECT_ROOT" ls-files | grep -qE '\.(keystore|jks)$|keystore\.properties'; then
  fail "signing material is tracked by git"; problems=$((problems + 1))
else
  ok "no signing material tracked by git"
fi

echo
if [ "$problems" -eq 0 ]; then echo "audit: clean"; exit 0; fi
echo "audit: $problems problem(s)" >&2
exit 1
