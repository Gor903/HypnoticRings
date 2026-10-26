#!/usr/bin/env bash
# Static hygiene. The release branch carries no scaffolding and no secrets — checked by
# command, because "I removed those" is exactly the claim that turns out to be false.
set -uo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/_common.sh"

problems=0
sources=$(find "$PROJECT_ROOT/Assets" -name '*.cs' 2>/dev/null)

scan() {
  local label="$1" pattern="$2"
  local hits
  hits=$(grep -nE "$pattern" $sources 2>/dev/null | grep -v '/Tests/' || true)
  if [ -n "$hits" ]; then
    fail "$label"
    echo "$hits" | sed "s|$PROJECT_ROOT/||" | head -20
    problems=$((problems + 1))
  else
    ok "$label"
  fi
}

echo "Source hygiene"
scan "no TODO / FIXME / HACK"      '(^|[^A-Za-z])(TODO|FIXME|HACK|XXX)([^A-Za-z]|$)'
scan "no debug logging"            'Debug\.Log(Warning|Error)?\s*\('
scan "no commented-out code"       '^\s*//\s*(if|for|while|return|var|public|private|void)\s'

echo
echo "Secrets"
scan "no keystore passwords"       '(keystorePass|keyaliasPass|storePassword|keyPassword)\s*=\s*"[^"]+"'
if git -C "$PROJECT_ROOT" ls-files | grep -qE '\.(keystore|jks)$|keystore\.properties'; then
  fail "signing material is tracked by git"; problems=$((problems + 1))
else
  ok "no signing material tracked by git"
fi

echo
if [ "$problems" -eq 0 ]; then echo "audit: clean"; exit 0; fi
echo "audit: $problems problem(s)" >&2
exit 1
