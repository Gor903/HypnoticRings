# Release build

> Template. Fill the paths and namespaces. Keep the reasoning — every
> warning here was paid for once already.

How to produce and check the artifact. Every number quoted here is read
from the artifact itself — never from a build log. A Unity build summary
once reported a 35 MB bundle as "664 MB" because the field it printed
measured the intermediate Gradle project.

## Tools

Unity ships all of them inside the Android player, so none of these need
installing:

```sh
U=~/Unity/Hub/Editor/{{UNITY_VERSION}}/Editor
AP=$U/Data/PlaybackEngines/AndroidPlayer
JAVA=$AP/OpenJDK/bin/java
KEYTOOL=$AP/OpenJDK/bin/keytool
BT=$(ls $AP/Tools/bundletool-all-*.jar)
AAPT=$(ls -d $AP/SDK/build-tools/*/ | tail -1)aapt2
APKSIGNER=$(ls $AP/SDK/build-tools/*/apksigner | tail -1)
```

### The SDK platform is the exception

The bundled SDK does **not** necessarily carry the platform matching
`ReleaseSettings.TargetSdk`, and no licences are pre-accepted. Gradle
then fails late — after the full IL2CPP compile — with "Failed to
install the following Android SDK packages as some licences have not
been accepted". It is an environment fault, not a project one, and
preflight cannot see it.

```sh
ls $AP/SDK/platforms/                       # must contain android-{{TARGET_API}}
SDKMGR=$AP/SDK/cmdline-tools/*/bin/sdkmanager
JAVA_HOME=$AP/OpenJDK $SDKMGR --sdk_root=$AP/SDK --licenses
JAVA_HOME=$AP/OpenJDK $SDKMGR --sdk_root=$AP/SDK --install "platforms;android-{{TARGET_API}}"
```

Check it **before** starting the build, not after losing half an hour.

## Signing

The keystore is **not** in this repository and never will be —
`.gitignore` excludes `*.keystore` and `*.jks`.

| | |
| --- | --- |
| Keystore | `{{KEYSTORE_PATH}}` |
| Backup | `{{KEYSTORE_BACKUP}}` |
| Credentials | `{{CREDENTIALS_PATH}}` (mode 600) |
| Alias | `{{KEY_ALIAS}}` |
| Certificate | `{{CERT_OWNER}}`, RSA 2048, 10000 days |

Credentials reach the build only through the environment. `ReleaseTool`
reads them and wipes them from `ProjectSettings` afterwards in a
`finally` block, so a failed build cannot leave a password in a tracked
file.

```sh
set -a; . {{CREDENTIALS_PATH}}; set +a
```

Without those variables the build still runs, but the output is named
`{{APP_SLUG}}-UNSIGNED.aab` so it cannot be mistaken for a releasable
artifact.

### Losing this key ends the app

No key means no updates, ever, and the application id cannot be changed
to start again. Keep at least one copy off this machine, and verify any
copy by **opening** it — a corrupt keystore is byte-identical in length
to a good one:

```sh
$KEYTOOL -list -v -keystore <copy> -storepass "${{GAME_UPPER}}_KEYSTORE_PASS"
```

A good copy prints `Entry type: PrivateKeyEntry`.

## Commands

An IL2CPP build for two architectures takes roughly 20–40 minutes from a
cold cache, holds every core, and will freeze the desktop if it is left
unbudgeted. Run it under a cgroup scope, detached, logging to a file —
the skill's `docs/resources.md` has the thresholds and the abort rules.

```sh
# Apply every release setting (menu: {{GAME_MENU}} → Release → Apply Settings)
$U/Unity -batchmode -nographics -projectPath . \
  -executeMethod {{NAMESPACE}}.Editor.ReleaseTool.CIApply -logFile apply.log

# Preflight only; exit 1 and a named list when anything is wrong
$U/Unity -batchmode -nographics -projectPath . \
  -executeMethod {{NAMESPACE}}.Editor.ReleaseTool.CIVerify -logFile verify.log

# Build; refuses to run when preflight fails
set -a; . {{CREDENTIALS_PATH}}; set +a
systemd-run --user --scope -q \
  -p MemoryHigh=8G -p MemoryMax=10G -p CPUQuota=600% \
  nice -n 10 \
  $U/Unity -batchmode -nographics -projectPath . \
    -executeMethod {{NAMESPACE}}.Editor.ReleaseTool.CIBuild -logFile build.log

# Every upload needs a higher version code — bumped here, never by hand
$U/Unity -batchmode -nographics -projectPath . \
  -executeMethod {{NAMESPACE}}.Editor.ReleaseTool.CIBumpVersionCode -logFile bump.log
```

Exit code alone does not prove success — batch mode can exit 0 after
logging a build failure:

```sh
grep -nE "error CS|BuildFailedException|Error building|Build Failed" build.log
ls -l --time-style=full-iso Build/{{APP_SLUG}}.aab   # timestamp from *this* run
```

`Library/Bee` makes later builds much faster. Do not delete it to "clean
up" — it is the difference between minutes and half an hour.

## Preflight

`ReleaseTool.Verify()` returns a list of problems and `Build()` refuses
on a non-empty list. It checks application id, product and company name,
min and target API, architectures, scripting backend, orientation lock,
bundle output, the three debug flags, the internet-permission setting,
the scene list, the icons, the version name and code — and that the
custom manifest still exists.

That last one is the subtle one. Deleting
`Assets/Plugins/Android/AndroidManifest.xml` silently restores the
network permission with no warning from anywhere, which would contradict
both the privacy policy and the Data safety answers.

The same file is also the only place the launcher activity is declared —
it replaces unityLibrary's manifest rather than merging into it, which
is why Unity's own template ships with both activity blocks and a
comment telling you to delete the unused one. A manifest without an
activity builds, signs and uploads cleanly, and produces an app with no
icon that cannot be started. Preflight therefore asserts both the
`LAUNCHER` category and `ReleaseSettings.ActivityClass`.

Prove preflight works in **both** directions: it passes on the clean
project, and on a deliberately broken one it names every fault and the
build produces nothing. A preflight that has only ever passed has not
been tested.

## Verifying the artifact

Run all of it after **any** change that touches the build — removing a
package can change the merged manifest.

```sh
# manifest, versions, SDK levels, full permission list
$JAVA -jar $BT dump manifest --bundle=Build/{{APP_SLUG}}.aab

# prove a permission is absent rather than eyeballing the list
$JAVA -jar $BT dump manifest --bundle=Build/{{APP_SLUG}}.aab | grep -c "permission.INTERNET"

# packaging: uncompressNativeLibraries should be enabled
$JAVA -jar $BT dump config --bundle=Build/{{APP_SLUG}}.aab

# size on disk and the largest entries
unzip -l Build/{{APP_SLUG}}.aab | sort -rn | head -10

# what a device actually downloads — not the bundle size
$JAVA -jar $BT build-apks --bundle=Build/{{APP_SLUG}}.aab --output=/tmp/app.apks
$JAVA -jar $BT get-size total --apks=/tmp/app.apks

# certificate owner must not be CN=Android Debug
$KEYTOOL -printcert -jarfile Build/{{APP_SLUG}}.aab
```

A mismatch on any line means the settings did not apply. It does not
mean the check is too strict.

## Sideload package for device testing

Built from the **same bundle that will be uploaded**, so the device run
covers the real artifact:

```sh
$JAVA -jar $BT build-apks --bundle=Build/{{APP_SLUG}}.aab \
  --output=/tmp/universal.apks --mode=universal
unzip -o -q /tmp/universal.apks -d /tmp/universal
cp /tmp/universal/universal.apk Build/{{APP_SLUG}}-universal-debugsigned.apk
```

Two things to know when handing it over:

- It is signed with the local debug key, so it **cannot** be installed
  over a store build, or vice versa. Check it with
  `$APKSIGNER verify --print-certs`, never with `keytool`: bundletool
  signs v2/v3 only, so `keytool` prints nothing at all and a correctly
  signed APK reads as unsigned.
- It is roughly twice the bundle size, because a universal APK carries
  every configuration and the native libraries are stored uncompressed.
  Store delivery uses per-device splits and is unaffected. Quote the
  split size when talking about download size.

## The device checklist

Short enough to finish in one sitting. Hand over the list, not a
description of what to look for:

1. Clean install, first launch — no crash, no missing icon.
2. Play to a win or a loss.
3. Kill the app mid-game, reopen — progress is where it was.
4. Background and return mid-action.
5. Rotate, if the game is not orientation-locked.
6. No debug interface anywhere.
7. Ten minutes continuous — note heat and battery.

If `adb devices` lists a phone, install it and hand back a running app
rather than a file. An empty list is the evidence that you could not,
and it gets said rather than glossed over.

Record the outcome as the human's report, never as your own observation.
