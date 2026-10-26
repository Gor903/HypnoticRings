#!/usr/bin/env bash
# Static hygiene. The release branch carries no scaffolding and no secrets — checked by
# command, because "I removed those" is exactly the claim that turns out to be false.
set -uo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/_common.sh"

problems=0
all_sources=$(find "$PROJECT_ROOT/Assets" -name '*.cs' 2>/dev/null)

# Code that reaches a player. HR.Editor is Editor-only in its asmdef and HR.Tests.* is gated
# on UNITY_INCLUDE_TESTS, so neither ships; within the remaining files, lint_sources.py drops
# anything behind an editor-only #if before looking.
runtime_sources=$(echo "$all_sources" | grep -v -e '/Editor/' -e '/Tests/' || true)

echo "Shipping code"
if [ -z "$runtime_sources" ]; then
  ok "no shipping sources yet"
elif python3 "$PROJECT_ROOT/tools/lint_sources.py" $runtime_sources; then
  ok "no debug logging, scaffolding markers or commented-out code"
else
  fail "findings in code that reaches a player"
  problems=$((problems + 1))
fi

# Markers are banned everywhere, editor tooling included — an unfinished editor script is
# still unfinished work in the release branch.
echo
echo "Everywhere"
markers=$(grep -nE '(^|[^A-Za-z])(TODO|FIXME|HACK|XXX)([^A-Za-z]|$)' $all_sources 2>/dev/null || true)
if [ -n "$markers" ]; then
  fail "scaffolding markers outside shipping code"
  echo "$markers" | sed "s|$PROJECT_ROOT/||" | head -20
  problems=$((problems + 1))
else
  ok "no TODO / FIXME / HACK anywhere"
fi

# The exclusion above is only honest while HR.Editor really is editor-only.
if grep -q '"Editor"' "$PROJECT_ROOT/Assets/_Project/Editor/HR.Editor.asmdef" 2>/dev/null; then
  ok "HR.Editor is Editor-only, so its logging cannot ship"
else
  fail "HR.Editor is no longer Editor-only — its logging would ship"
  problems=$((problems + 1))
fi

echo
echo "Secrets"
secrets=$(grep -nE '(keystorePass|keyaliasPass|storePassword|keyPassword)[[:space:]]*=[[:space:]]*"[^"]+"' \
          $all_sources 2>/dev/null || true)
if [ -n "$secrets" ]; then
  fail "hardcoded keystore passwords"
  echo "$secrets" | sed "s|$PROJECT_ROOT/||"
  problems=$((problems + 1))
else
  ok "no hardcoded keystore passwords"
fi

if git -C "$PROJECT_ROOT" ls-files | grep -qE '\.(keystore|jks)$|keystore\.properties'; then
  fail "signing material is tracked by git"
  problems=$((problems + 1))
else
  ok "no signing material tracked by git"
fi

echo
if [ "$problems" -eq 0 ]; then echo "audit: clean"; exit 0; fi
echo "audit: $problems problem(s)" >&2
exit 1
