# Publishing — {{GAME}}

> Template. Replace every `{{TOKEN}}`, delete every line that is not true
> of this game, and write the prose in the language of the design
> documents. `check-listing.py` fails while any token survives — a
> half-filled template reads as a finished document, which is worse than
> no document.

The one place to answer "can we ship yet". Keep it true.

**Current answer: {{SHIP_ANSWER}}.** Everything outstanding is under
[Left before upload](#left-before-upload).

Last verified end to end on {{DATE}}: {{EDIT_TESTS}} edit-mode and
{{PLAY_TESTS}} play-mode tests green, bundle manifest re-dumped, policy
URL re-fetched.

## Automation

| Menu | What it does |
| --- | --- |
| {{GAME_MENU}} → Release → Apply Settings | Sets every platform setting |
| {{GAME_MENU}} → Release → Verify | Lists what blocks a release build |
| {{GAME_MENU}} → Release → Build Bundle | Release build into `Build/` |
| {{GAME_MENU}} → Art → Generate All | Regenerates icons and store art |
| {{GAME_MENU}} → Art → Capture Screenshots | Regenerates the screenshot set |

Command-line equivalents, including the version-code bump, are in
`release-build.md`. Nothing here is a checkbox someone has to remember
ticking — the build refuses to run if any of it drifts.

## Name

| Where | Value |
| --- | --- |
| In game (title screen) | {{TITLE_IN_GAME}} |
| Store listing, {{LOCALE}} | {{STORE_NAME}} |
| Launcher label, package, artifact | {{LAUNCHER_LABEL}} |
| Application id | `{{APPLICATION_ID}}` |

The launcher label is one string for every language — Android does not
localise it, so seeing the same text across all locales is correct.

**The application id is fixed permanently by the first upload.**

### Name research actually performed

{{NAME_RESEARCH}}

> Say which stores and which registries were searched, and by what means.
> Then say plainly what was *not* done. Store search finding nothing is
> not a trademark search, and the difference is the whole value of this
> section. List any candidate rejected on a collision and what it
> collided with.

## Done

Only tick a line when a command proved it. Each of these is verified
against the artifact or by command output, never from a build log — a
Unity build summary once reported a 35 MB bundle as "664 MB".

- [ ] Platform target, bundle output, development build off
- [ ] Scripting backend, architectures, stripping level
- [ ] Application id
- [ ] Target API {{TARGET_API}}, minimum API {{MIN_API}} — target verified
      against Google's own current requirement, not taken from a comment
- [ ] Version name {{VERSION_NAME}}, version code {{VERSION_CODE}}
- [ ] App icon (adaptive, layered)
- [ ] Store icon 512×512 — verified fully opaque
- [ ] Feature graphic 1024×500 — verified fully opaque
- [ ] Screenshots — {{SHOT_COUNT}} frames, {{SHOT_SIZE}}, from the real interface
- [ ] Interface localised, every string in one table, zero literals in views
- [ ] Only the shipping scene in the build list
- [ ] Orientation lock and safe-area rendering
- [ ] Tests green — {{EDIT_TESTS}} edit-mode, {{PLAY_TESTS}} play-mode,
      from the command line
- [ ] Privacy policy written, carrying a real support address —
      `check-listing.py` passes every measurable check
- [ ] Artifact builds — `Build/{{APP_SLUG}}.aab`, {{BUNDLE_BYTES}} bytes
- [ ] Artifact signed with the release key, not the debug key —
      `{{CERT_OWNER}}`
- [ ] Debug symbols exported — {{SYMBOLS_BYTES}} bytes
- [ ] Permission list verified against the artifact — `{{PERMISSION_LIST}}`
      is the whole list, `INTERNET` and `ACCESS_NETWORK_STATE` both count 0
- [ ] Download size measured from the artifact — {{DOWNLOAD_SIZE}} per
      device, which is not the bundle size
- [ ] Dependencies trimmed to what the game references, tests still green
- [ ] Launcher activity declared — the custom manifest *replaces*
      unityLibrary's rather than adding to it, so omitting the activity
      ships an app that installs with no icon and cannot be started
- [ ] Sideload APK rebuilt from the shipping bundle — `libil2cpp.so`
      hashes match, so the device run exercises the uploaded artifact

### Permissions

| Permission | Why |
| --- | --- |
| `{{PERMISSION}}` | {{PERMISSION_REASON}} |

That is the whole list. Unity injects `INTERNET` and
`ACCESS_NETWORK_STATE` by itself even when the build opens no socket;
both are stripped by `Assets/Plugins/Android/AndroidManifest.xml` using
`tools:node="remove"`.

**Deleting that manifest silently restores them**, with no warning from
anywhere — which would contradict both the privacy policy and the Data
safety answers. Its existence is therefore a preflight check, and the
build refuses to run without it.

That file *replaces* unityLibrary's manifest rather than merging into
it, so it also has to declare the launcher activity. Without one the app
installs with no icon and no way to start, and every other check still
passes — which is why preflight asserts the activity too.

## Decisions for the human

### Language

Default **{{LOCALE}}**. The interface reads the system locale at startup
and falls back to {{LANGUAGE}}, so shipping to all countries is safe.
Adding a language is a new table in `Strings.cs` and a new listing — no
code changes.

### Signing key

| | |
| --- | --- |
| Keystore | `{{KEYSTORE_PATH}}` |
| Backup | `{{KEYSTORE_BACKUP}}` |
| Credentials | `{{CREDENTIALS_PATH}}`, mode 600 |
| Alias | `{{KEY_ALIAS}}` |
| Certificate | `{{CERT_OWNER}}`, RSA 2048 |
| Valid until | {{CERT_EXPIRY}} |
| Last verified | {{DATE}}, opened with `keytool -list`; the fingerprint matches the one inside the signed bundle |

Verify any new copy by opening it — a corrupt keystore is byte-identical
in length to a good one. That a copy reached an off-machine disk intact
is the one fact here this machine cannot check for itself; record it as
the user's report, not as verification.

### Monetisation

{{MONETISATION}}

> State free or paid, purchases yes or no, and ads yes or no — each with
> the reason drawn from what is or is not compiled in. If a plan
> describes ads for a later version, name the six things that move
> together when they are switched on: the requires-internet constant, the
> manifest entry, the ads declaration, the advertising-ID declaration,
> the Data safety form, and the privacy policy.

### Everything else in the console

- Privacy policy hosting — {{PRIVACY_HOST}}, and it must outlive publication
- Content rating, data safety, target audience — answers in
  `play-console-answers.md`
- Testing track requirements — read the current rule in the console

Record which address is the console account and which is the public
support contact. They are easy to confuse, and one of them is visible to
every player.

## Left before upload

- [ ] **Real device run** — install
      `Build/{{APP_SLUG}}-universal-debugsigned.apk`, rebuilt on {{DATE}}
      from the shipping bundle. Editor captures have no status bar,
      cutout or gesture bar and are not a test
- [ ] **Put the policy URL in both console fields.** The page is live at
      {{PRIVACY_URL}} — re-verified {{DATE}}. A 404 here after publication
      is grounds for removal, not just rejection
- [ ] **Signing key backed up off this machine**
- [ ] **Closed testing period** — weeks of calendar, and the critical
      path. Recruit testers before polishing anything
- [ ] Submit listing, forms and artifact — steps in `play-console-steps.md`

## Worth doing, not blocking

- {{DEFERRED_WORK}}
