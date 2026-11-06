#!/usr/bin/env bash
# Reads an APK and says what is actually in it.
#
# The sibling of verify_artifact.sh, which reads the bundle. Same questions, different
# tools: a bundle is opened by bundletool, an APK by aapt2 and apksigner. Reached through
# verify_artifact.sh, which dispatches on the extension — call that one.
#
# Why an APK is verified at all, when the store only ever sees the bundle: the APK is the
# one that reaches a hand. A throwaway signing key, a permission that crept back in with a
# package, a debuggable flag left on — all of them travel to a phone in this file, and the
# build log will not mention any of them.
#
#   tools/verify_apk.sh [path-to-apk]
set -uo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/_common.sh"

apk="${1:-$BUILD_DIR/hypnoticrings.apk}"
problems=0

note()  { printf '  %-44s %s\n' "$1" "$2"; }
good()  { printf '  \033[32mok\033[0m    %-38s %s\n' "$1" "$2"; }
bad()   { printf '  \033[31mFAIL\033[0m  %-38s %s\n' "$1" "$2"; problems=$((problems+1)); }

[ -f "$apk" ] || { fail "no apk at $apk"; exit 1; }

SDK="$UNITY_ROOT/Data/PlaybackEngines/AndroidPlayer/SDK"
BT=$(ls -d "$SDK"/build-tools/*/ 2>/dev/null | sort -V | tail -1)
AAPT="$BT/aapt2"
SIGNER="$BT/apksigner"

for tool in "$AAPT" "$SIGNER"; do
  [ -x "$tool" ] || { fail "missing $tool"; exit 1; }
done

echo "Artifact"
note "path" "${apk#$PROJECT_ROOT/}"
note "size" "$(du -h "$apk" | cut -f1)"
note "modified" "$(stat -c '%y' "$apk" | cut -c1-19)"
note "read with" "$(basename "$(dirname "$AAPT")") build-tools"

badging=$("$AAPT" dump badging "$apk" 2>/dev/null)
[ -n "$badging" ] || { fail "aapt2 could not read the manifest"; exit 1; }

echo
echo "Identity"
pkg=$(grep -oP "^package: name='\K[^']+" <<<"$badging")
vcode=$(grep -oP "versionCode='\K[^']+" <<<"$badging" | head -1)
vname=$(grep -oP "versionName='\K[^']+" <<<"$badging" | head -1)
label=$(grep -oP "^application-label:'\K[^']+" <<<"$badging")

# A throwaway id is a legitimate answer for an MVP build and a wrong one for anything else,
# so it is reported rather than judged — the caller knows which stage it is in.
case "$pkg" in
  com.gor903.hypnoticrings) good "package" "$pkg" ;;
  *.mvp|com.example.*)      note "package" "$pkg  (throwaway — MVP only, never upload)" ;;
  *)                        bad  "package" "$pkg" ;;
esac
note "versionCode" "$vcode"
note "versionName" "$vname"
note "label" "$label"

echo
echo "SDK levels"
# aapt2 prints minSdkVersion; the older aapt printed it as plain sdkVersion. Accept both,
# because reading the wrong key returns empty and an empty value fails as loudly as a wrong
# one while meaning something completely different.
minsdk=$(grep -oP "^minSdkVersion:'\K[^']+" <<<"$badging")
[ -n "$minsdk" ] || minsdk=$(grep -oP "^sdkVersion:'\K[^']+" <<<"$badging")
tgtsdk=$(grep -oP "^targetSdkVersion:'\K[^']+" <<<"$badging")
[ "$minsdk" = "26" ] && good "minSdkVersion" "$minsdk" || bad "minSdkVersion" "$minsdk (want 26)"
[ "$tgtsdk" = "36" ] && good "targetSdkVersion" "$tgtsdk" || bad "targetSdkVersion" "$tgtsdk (want 36)"

echo
echo "Permissions"
# The list stays closed, it just changed: ads need the network, anything about the
# advertising id stays out. AD_ID and ACCESS_ADSERVICES_AD_ID are two names for the same want.
for forbidden in "permission.AD_ID" "ACCESS_ADSERVICES_AD_ID"; do
  n=$(grep -c "$forbidden" <<<"$badging")
  [ "$n" -eq 0 ] && good "$forbidden absent" "0 occurrences" || bad "$forbidden present" "$n occurrence(s)"
done

for wanted in INTERNET ACCESS_NETWORK_STATE VIBRATE; do
  n=$(grep -c "permission.$wanted" <<<"$badging")
  [ "$n" -ge 1 ] && good "$wanted present" "declared" || bad "$wanted missing" "the build needs it"
done

# Everything the ads SDK brings is named here, so a new one trips this and gets looked at.
unexpected=$(grep -oP "^uses-permission: name='\K[^']+" <<<"$badging" \
  | grep -vxE "android.permission.(INTERNET|ACCESS_NETWORK_STATE|VIBRATE|WAKE_LOCK|FOREGROUND_SERVICE|ACCESS_ADSERVICES_(ATTRIBUTION|TOPICS))|com\..*DYNAMIC_RECEIVER_NOT_EXPORTED_PERMISSION" || true)
[ -z "$unexpected" ] && good "nothing rode along" "every permission is one we named" \
  || bad "unnamed permissions" "$(tr '\n' ' ' <<<"$unexpected")"

echo
echo "  every uses-permission line in the apk:"
grep -oP "^uses-permission: name='\K[^']+" <<<"$badging" | sed 's/^/    /' || echo "    (none)"

# An empty app id is not a missing feature, it is a crash: the SDK brings up its own
# ContentProvider at app start and aborts before a line of our code runs.
appid=$("$AAPT" dump xmltree --file AndroidManifest.xml "$apk" 2>/dev/null \
  | grep -A1 "ads.APPLICATION_ID" | grep -oP 'value[^=]*="\K[^"]+' | head -1)
case "$appid" in
  ca-app-pub-*~*) good "AdMob app id" "$appid" ;;
  "")             bad  "AdMob app id missing" "the SDK aborts the app on start" ;;
  *)              bad  "AdMob app id malformed" "$appid" ;;
esac

echo
echo "Packaging"
if grep -q "application-debuggable" <<<"$badging"; then
  bad "debuggable" "a release build must not be"
else
  good "not debuggable" "absent from the manifest"
fi

tree=$("$AAPT" dump xmltree --file AndroidManifest.xml "$apk" 2>/dev/null)
grep -qE 'screenOrientation\(0x[0-9a-f]+\)=1$' <<<"$tree" \
  && good "portrait locked" "screenOrientation=1" \
  || note "screenOrientation" "$(grep -oP 'screenOrientation\(0x[0-9a-f]+\)=\K.*' <<<"$tree" | head -1)"
grep -qE 'resizeableActivity\(0x[0-9a-f]+\)=false' <<<"$tree" \
  && good "resizeableActivity" "false — portrait game, no window mode" \
  || note "resizeableActivity" "true"

echo
echo "Signature"
sig=$("$SIGNER" verify --print-certs -v "$apk" 2>&1)
owner=$(grep -oP '^Signer #1 certificate DN: \K.*' <<<"$sig" | head -1)
sha=$(grep -oP '^Signer #1 certificate SHA-256 digest: \K.*' <<<"$sig" | head -1)

if grep -q "DOES NOT VERIFY" <<<"$sig" || [ -z "$owner" ]; then
  bad "unsigned or broken" "apksigner refused it"
elif grep -qi "Android Debug" <<<"$owner"; then
  bad "debug key" "$owner"
else
  good "signed by" "$owner"
  note "SHA-256" "$sha"
fi

for scheme in v2 v3; do
  grep -qE "^Verified using $scheme scheme[^:]*: true" <<<"$sig" \
    && good "scheme $scheme" "present" \
    || note "scheme $scheme" "absent"
done

echo
echo "Native libraries"
libs=$(grep -oP "^native-code: \K.*" <<<"$badging" | tr -d "'" )
note "declared" "${libs:-none}"
actual=$(unzip -l "$apk" | grep -oP 'lib/\K[^/]+' | sort -u)
echo "$actual" | sed 's/^/    /'
if [ "$(echo "$actual" | wc -l)" -eq 1 ] && [ "$actual" = "arm64-v8a" ]; then
  good "arm64-v8a only" "no second native library riding along"
else
  bad "unexpected ABIs" "$(echo $actual)"
fi

echo
echo "Contents"
total=$(unzip -l "$apk" | tail -1)
note "entries" "$(echo "$total" | awk '{print $2}') files, $(echo "$total" | awk '{print $1}') bytes uncompressed"
echo "  largest entries:"
unzip -l "$apk" | sort -rn | head -6 | awk '{printf "    %10s  %s\n", $1, $4}'

echo
if [ "$problems" -eq 0 ]; then ok "apk: clean"; exit 0; fi
fail "apk: $problems problem(s)"; exit 1
