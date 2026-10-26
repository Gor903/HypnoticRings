# Console submission, step by step

> Template. This is **the** file the human opens. If they have to read
> anything else to answer a field, this file is not finished. Fill every
> token from the artifact, not from the plan.

Everything the human pastes, in the order the console asks for it.

Menu labels change without notice. If wording has drifted, match by
meaning — the order of steps does not depend on it.

Every number here was read from the artifact on {{DATE}}, never from a
build log. Re-read them with the commands in `release-build.md` after
any rebuild; a rebuild changes sizes and invalidates the sideload APK.

**Order matters more than polish.** Steps 1–4 are one evening. Step 5 is
weeks of calendar nobody can shorten, so the point of this order is to
reach Step 5 fast and polish while the clock runs.

## Keep at hand

| What | Where |
| --- | --- |
| Bundle | `Build/{{APP_SLUG}}.aab` — {{BUNDLE_BYTES}} bytes |
| Debug symbols | `Build/{{SYMBOLS_FILE}}` — {{SYMBOLS_BYTES}} bytes |
| Sideload APK | `Build/{{APP_SLUG}}-universal-debugsigned.apk` — {{APK_BYTES}} bytes |
| Store icon | `docs/publishing/art/store-icon-512.png` — 512×512 |
| Feature graphic | `docs/publishing/art/feature-graphic-1024x500.png` — 1024×500 |
| Screenshots | `docs/publishing/art/screenshots/` — {{SHOT_COUNT}}, {{SHOT_SIZE}} |
| Privacy policy | {{PRIVACY_URL}} |
| Support email | `{{SUPPORT_EMAIL}}` |

Build facts the console may ask for, all taken from the artifact:

| Fact | Value |
| --- | --- |
| Application id | `{{APPLICATION_ID}}` |
| Version name | {{VERSION_NAME}} |
| Version code | {{VERSION_CODE}} |
| Minimum API | {{MIN_API}} |
| Target API | {{TARGET_API}} |
| Architectures | {{ARCHITECTURES}} |
| Permissions | `{{PERMISSION_LIST}}` — the whole list |
| Download size | {{DOWNLOAD_SIZE}} per device, **not** the bundle size |

Signing fingerprint to compare after upload:

```
SHA-256: {{SHA256}}
SHA-1:   {{SHA1}}
```

Read back with `keytool -printcert -jarfile Build/{{APP_SLUG}}.aab`. The
owner must be `{{CERT_OWNER}}` — anything reading `CN=Android Debug` is
the wrong artifact.

## Before you open the console

- [ ] Install the sideload APK on a real phone and play it. Editor
      captures have no status bar, cutout or gesture bar and are not a
      test
- [ ] Confirm the signing key copy on the separate disk still opens —
      `keytool -list -v -keystore <copy>`, never a file-size comparison

## Step 1 — Create app

| Field | Value |
| --- | --- |
| App name | `{{STORE_NAME}}` |
| Default language | {{LOCALE}} |
| App or game | Game |
| Free or paid | {{PRICE_ANSWER}} |

Irreversible here, all three:

- **Free → paid is one-way.** A free app can never become paid.
- **The default language** sets the primary listing locale permanently.
- **The application id** `{{APPLICATION_ID}}` is fixed by the first
  upload and can never be changed or reused.

Tick the declarations about developer-programme policies and US export
law.

### Check the application id letter by letter

It ends up in the store address —
`play.google.com/store/apps/details?id=…` — where it is public and
permanent. Read it against the artifact rather than against memory:

```sh
bundletool dump manifest --bundle=Build/{{APP_SLUG}}.aab | head -1   # package="…"
```

A single mistyped character has reached this field before. The upload
rejects it, which is the good outcome; after publication there is no fix
at all.

## Step 2 — App content

One section per form, each with its own Save. The basis for every answer
is in `play-console-answers.md`; it is not restated here.

### Privacy policy

Paste the URL into **App content → Privacy policy**. It also goes into
**Store listing → Privacy policy** at Step 3 — two separate fields, same
URL, and the listing one is easy to miss.

```
{{PRIVACY_URL}}
```

The page must stay reachable after publication. A 404 here is grounds
for removal, not merely rejection.

### App access

**{{APP_ACCESS}}**

### Ads

**{{ADS_DECLARATION}}**

### Content rating

Start the questionnaire. Email: the support address. Category: **Game**.

| Question | Answer |
| --- | --- |
| Violence, blood, fear | {{Q_VIOLENCE}} |
| Sexual content, nudity | {{Q_SEXUAL}} |
| Profanity | {{Q_PROFANITY}} |
| Drugs, alcohol, tobacco | {{Q_DRUGS}} |
| Gambling and simulated gambling | {{Q_GAMBLING}} |
| Real-money purchases | {{Q_PURCHASES}} |
| Data sharing, location | {{Q_DATA}} |
| User interaction, user-generated content | {{Q_SOCIAL}} |

Expected result: **{{EXPECTED_RATING}}**. Submit the questionnaire — the
rating is not applied until you do.

### Target audience and content

| Field | Answer |
| --- | --- |
| Age groups | {{AGE_GROUPS}} |
| Appeals to children by design | {{APPEALS_TO_CHILDREN}} |

### Data safety

| Question | Answer |
| --- | --- |
| Does the app collect or share user data? | **{{COLLECTS_DATA}}** |
| Encryption in transit | {{ENCRYPTION_ANSWER}} |
| Deletion on request | {{DELETION_ANSWER}} |

Expected result: **No data collected** and **No data shared**.

### Remaining declarations

| Declaration | Answer |
| --- | --- |
| Advertising ID | **{{AD_ID_ANSWER}}** — `AD_ID` is not in the manifest |
| Restricted permissions | {{RESTRICTED_PERMISSIONS}} |
| Background location | {{Q_LOCATION}} |
| Health, finance, government, news | {{Q_SPECIAL_CATEGORY}} |

## Step 3 — Store listing

**Main store listing → {{LOCALE}}.**

App name — {{NAME_COUNT}} of 30 characters:

```
{{STORE_NAME}}
```

Short description — {{SHORT_COUNT}} of 80 characters:

```
{{SHORT_DESCRIPTION}}
```

Full description — {{FULL_COUNT}} of 4000 characters. Copy it from the
fenced block in `store-listing.md`. It lives in exactly one file on
purpose: two copies of a long block drift, and `check-listing.py`
measures only that one.

Graphics:

| Asset | File |
| --- | --- |
| App icon | `art/store-icon-512.png` |
| Feature graphic | `art/feature-graphic-1024x500.png` |
| Phone screenshots | `art/screenshots/`, uploaded in filename order |

Store settings:

| Field | Value |
| --- | --- |
| App category | {{CATEGORY}} |
| Tags | {{TAGS}} |
| Support email | `{{SUPPORT_EMAIL}}` |
| Privacy policy | the URL from Step 2 — this field is separate |

Run `python3 docs/publishing/check-listing.py` before pasting. It measures
every limit above rather than trusting the counts written here.

> Additional locales: **Manage translations → Add your own translation**.
> Icon and feature graphic are shared; screenshots are per-locale when
> the interface carries text.

## Step 4 — Internal testing

Internal testing has no tester-count or duration requirement, so it is
the fastest way to prove the upload pipeline and the real signed
install. Do it the same evening.

1. **Testing → Internal testing → Create new release.**
2. Accept **Play App Signing**. This is where the developer key becomes
   an *upload* key and losing it stops being fatal — Google holds the
   distribution key from here on. Keep the upload key anyway: it is how
   every future release is authenticated.
3. Upload `Build/{{APP_SLUG}}.aab`.
4. Upload `Build/{{SYMBOLS_FILE}}` so crash traces deobfuscate. Without
   the symbols, crash stacks in the console are unreadable.
5. Release name `{{VERSION_NAME}} ({{VERSION_CODE}})`. Release notes: the
   block under **Release notes** in `store-listing.md` — {{NOTES_COUNT}}
   of the 500 characters allowed. The field arrives wrapped in `<en-US>`
   tags; paste between them and leave the tags.
6. After processing, open **App integrity** and compare the **upload
   key** certificate against the SHA-256 above. They must match. The
   **app signing key** will be a different fingerprint — that is
   Google's, and is correct.
7. Add yourself as a tester, follow the opt-in link, then **install from
   the store link**, not by sideloading. That exercises the real
   signature and per-device delivery.

Expect the download to be roughly {{DOWNLOAD_SIZE}}, not the bundle size.

> A sideloaded debug-signed build of the same package blocks the Play
> install with "another user has already installed an incompatible
> version" — the signatures differ. Uninstall it from **every** user
> profile on the phone first.

## Step 5 — Closed testing (the long pole)

**This is the critical path, and it is measured in weeks.** A personal
developer account created after 13 November 2023 cannot reach Production
until it has run a closed test with a minimum number of testers opted in
continuously for a minimum period.

Read the current count and duration off the console rather than a
remembered number — Google has changed both more than once.

- [ ] Create a **Closed testing** track and a release with the same bundle.
- [ ] Recruit real Google accounts and get every one of them to **opt in
      via the invite link**. Being on the email list is not enough — they
      must accept.
- [ ] The clock starts the day the last required tester opts in, and they
      must stay opted in. Opting out and back in does not bank the
      earlier days; that tester restarts from zero. Recruit more than the
      minimum.
- [ ] Check the console counter rather than your own arithmetic.
- [ ] While the clock runs: gather feedback, fix what matters, ship
      updates to the same track. Bump the version code first — it must
      increase on every upload.

Start recruiting before polishing anything else. Every other item in
this document is an evening of work; this one is the calendar.

## Step 6 — Production

Only available once Step 5's requirement is satisfied and the console
says so.

1. Apply for production access. Expect a short questionnaire about the
   testing you ran.
2. **Production → Create new release.** Promote the tested bundle rather
   than uploading a fresh one, so the artifact that ships is the
   artifact that was tested.
3. Countries: all. The interface falls back to {{LANGUAGE}} for any
   unshipped locale, so no country gets a broken build.
4. Rollout percentage: {{ROLLOUT}}.
5. Submit for review.

If the bundle is ever rebuilt before this step, the version code must be
higher. Bump it with the release script, never by hand.

## After launch

- Keep {{PRIVACY_URL}} alive. A 404 there is grounds for removal.
- Re-submit Data safety and the ads declaration the moment ads,
  analytics or leaderboards are added. Today's answers are true because
  of what is *not* in the build.
- Target API requirements move every year. Expect to raise the target
  SDK annually to keep shipping updates.

## If the console objects

| Objection | Cause | Fix |
| --- | --- | --- |
| Version code already used | A build with that code was uploaded before | Bump, rebuild, re-upload |
| Debuggable / development build | `Development Build` left on | Preflight catches this; re-run Apply Settings and rebuild |
| Package name already in use | The application id is taken globally | Nothing to do but choose another — and it is fixed forever at first upload |
| "The package must be named …" | The console entry's id differs from the bundle's | Fix the **console**, not the bundle, when the bundle is right: delete the draft app and create it again. A deleted app with zero installs frees its name; one with installs never does |
| Privacy policy unreachable | The host is down or the URL moved | Re-deploy from `docs/publishing/privacy`; verify 200 in an incognito window before resubmitting |
| Target API too low | Google raised the floor | Raise the target SDK, install the SDK platform, rebuild |
| Data safety mismatch | A permission appeared that the form does not declare | Re-dump the manifest; the form follows the artifact, not the other way round |

Write the project's own here as they come up.
