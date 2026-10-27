#!/usr/bin/env bash
# Reads the bundle and says what is actually in it.
#
# Nothing here looks at a build log. Build tools report on themselves and get it
# wrong; the only thing that knows what shipped is the file that shipped. Every
# number this prints comes out of the .aab.
#
#   tools/verify_artifact.sh [path-to-aab]
set -uo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/_common.sh"

aab="${1:-$PROJECT_ROOT/Build/hypnoticrings.aab}"
problems=0

note()  { printf '  %-44s %s\n' "$1" "$2"; }
good()  { printf '  \033[32mok\033[0m    %-38s %s\n' "$1" "$2"; }
bad()   { printf '  \033[31mFAIL\033[0m  %-38s %s\n' "$1" "$2"; problems=$((problems+1)); }

[ -f "$aab" ] || { fail "no bundle at $aab"; exit 1; }

AP="$UNITY_ROOT/Data/PlaybackEngines/AndroidPlayer"
JAVA="$AP/OpenJDK/bin/java"
KEYTOOL="$AP/OpenJDK/bin/keytool"
BT=$(ls "$AP"/Tools/bundletool-all-*.jar 2>/dev/null | head -1)

for tool in "$JAVA" "$KEYTOOL"; do
  [ -x "$tool" ] || { fail "missing $tool"; exit 1; }
done
[ -n "$BT" ] || { fail "no bundletool under $AP/Tools"; exit 1; }

echo "Artifact"
note "path" "${aab#$PROJECT_ROOT/}"
note "size" "$(du -h "$aab" | cut -f1)"
note "modified" "$(stat -c '%y' "$aab" | cut -c1-19)"

manifest=$("$JAVA" -jar "$BT" dump manifest --bundle="$aab" 2>/dev/null)
[ -n "$manifest" ] || { fail "bundletool could not read the manifest"; exit 1; }

echo
echo "Identity"
pkg=$(grep -oP 'package="\K[^"]+' <<<"$manifest" | head -1)
vcode=$(grep -oP 'versionCode="\K[^"]+' <<<"$manifest" | head -1)
vname=$(grep -oP 'versionName="\K[^"]+' <<<"$manifest" | head -1)
[ "$pkg" = "com.gor903.hypnoticrings" ] && good "package" "$pkg" || bad "package" "$pkg"
note "versionCode" "$vcode"
note "versionName" "$vname"

echo
echo "SDK levels"
minsdk=$(grep -oP 'minSdkVersion="\K[^"]+' <<<"$manifest" | head -1)
tgtsdk=$(grep -oP 'targetSdkVersion="\K[^"]+' <<<"$manifest" | head -1)
[ "$minsdk" = "26" ] && good "minSdkVersion" "$minsdk" || bad "minSdkVersion" "$minsdk (want 26)"
[ "$tgtsdk" = "36" ] && good "targetSdkVersion" "$tgtsdk" || bad "targetSdkVersion" "$tgtsdk (want 36)"

echo
echo "Permissions"
# Proved absent by counting, not by reading the list and believing it is complete.
for forbidden in INTERNET ACCESS_NETWORK_STATE AD_ID; do
  n=$(grep -c "permission.$forbidden" <<<"$manifest")
  [ "$n" -eq 0 ] && good "$forbidden absent" "0 occurrences" || bad "$forbidden present" "$n occurrence(s)"
done
n=$(grep -c "permission.VIBRATE" <<<"$manifest")
[ "$n" -ge 1 ] && good "VIBRATE present" "haptics need it (G-09)" || bad "VIBRATE missing" "spec 11.4 needs it"

echo
echo "  every uses-permission line in the bundle:"
grep -oP 'uses-permission[^>]*android:name="\K[^"]+' <<<"$manifest" | sed 's/^/    /' || echo "    (none)"

echo
echo "Orientation and packaging"
grep -qE 'screenOrientation="(portrait|1|0x1)"' <<<"$manifest" \
  && good "portrait locked" "from the manifest" \
  || note "screenOrientation" "$(grep -oP 'screenOrientation="\K[^"]+' <<<"$manifest" | head -1)"
grep -qiE 'extractNativeLibs="(false|0x0)"' <<<"$manifest" \
  && good "extractNativeLibs" "false — libraries stay compressed in place" \
  || bad "extractNativeLibs" "true; the install unpacks libs and doubles its footprint"

echo
grep -qiE 'resizeableActivity="(false|0x0)"' <<<"$manifest" \
  && good "resizeableActivity" "false — portrait game, no window mode" \
  || note "resizeableActivity" "true"

echo "Signature"
cert=$("$KEYTOOL" -printcert -jarfile "$aab" 2>/dev/null)
owner=$(grep -oP 'Owner: \K.*' <<<"$cert" | head -1)
sha=$(grep -oP 'SHA256: \K.*' <<<"$cert" | head -1)
if [ -z "$owner" ]; then
  bad "unsigned" "no JAR signature in the bundle"
elif grep -qi "Android Debug" <<<"$owner"; then
  bad "debug key" "$owner"
else
  good "signed by" "$owner"
  note "SHA-256" "$sha"
  echo "    ^ the human compares this in Play Console after upload"
fi

echo
echo "Contents"
total=$(unzip -l "$aab" | tail -1)
note "entries" "$(echo "$total" | awk '{print $2}') files, $(echo "$total" | awk '{print $1}') bytes uncompressed"
echo "  largest entries:"
unzip -l "$aab" | sort -rn | head -6 | awk '{printf "    %10s  %s\n", $1, $4}'

echo
echo "Native libraries"
libs=$(unzip -l "$aab" | grep -oP 'lib/\K[^/]+' | sort -u)
echo "$libs" | sed 's/^/    /'
if [ "$(echo "$libs" | wc -l)" -eq 1 ] && [ "$libs" = "arm64-v8a" ]; then
  good "arm64-v8a only" "no second native library riding along"
else
  bad "unexpected ABIs" "$(echo $libs)"
fi

echo
echo "Delivery size"
apks=$(mktemp -u /tmp/hr-XXXX.apks)
if "$JAVA" -jar "$BT" build-apks --bundle="$aab" --output="$apks" --overwrite >/dev/null 2>&1; then
  "$JAVA" -jar "$BT" get-size total --apks="$apks" 2>/dev/null | sed 's/^/    /'
  rm -f "$apks"
else
  note "get-size" "bundletool could not build apks (needs a signing config)"
fi

echo
if [ "$problems" -eq 0 ]; then ok "artifact: clean"; exit 0; fi
fail "artifact: $problems problem(s)"; exit 1
