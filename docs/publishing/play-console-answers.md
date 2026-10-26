# Console form answers

> Template. Every answer here must follow from what the build actually
> does. An answer copied from another game is a false declaration, and
> Data safety is the form that gets apps removed.

If what the build does stops being true — an ad network appears,
analytics go in, a leaderboard is added — these answers change and Data
safety has to be resubmitted.

## Basis

Verified against the artifact, not from memory. See
`docs/publishing/release-build.md` for the commands and their output.

| Fact | How it was established |
| --- | --- |
| {{BASIS_FACT}} | {{BASIS_EVIDENCE}} |

> One row per claim the forms below rest on: no network code, no
> `INTERNET` permission, the full permission list, no analytics or crash
> reporting, where storage lives, no purchases. Each row names the
> command or the file that proves it, so the answers can be rechecked
> rather than taken on trust.

The save file holds exactly: {{SAVE_CONTENTS}}. {{IDENTIFIERS}}

> "No identifier of any kind is generated or stored" is an answer that
> has to be true. Check before writing it.

## Store listing

- **Category:** {{CATEGORY}}
- **Tags:** {{TAGS}}
- **Contains ads:** **{{ADS_ANSWER}}** — {{ADS_REASON}}
- **In-app purchases:** **{{IAP_ANSWER}}**
- **Free or paid:** **{{PRICE_ANSWER}}** — free → paid is not reversible

## App access

**{{APP_ACCESS}}**

> State what a reviewer reaches from a clean install, whether anything
> starts locked and how it unlocks, and whether credentials must be
> supplied. Cosmetic unlocks that cost nothing are still "available
> without special access".

## Ads

**{{ADS_DECLARATION}}**

When ads are added, the following all change together: this answer, the
ads declaration, the advertising-ID declaration, the Data safety form,
the `INTERNET` permission, and the privacy policy.

## Data safety

- **Does the app collect user data?** **{{COLLECTS_DATA}}**
- **Does it share data with third parties?** **{{SHARES_DATA}}**
- **Encryption in transit:** {{ENCRYPTION_ANSWER}}
- **Deletion on request:** {{DELETION_ANSWER}}

Data that never leaves the device and goes away with the app is not
"collection" in this form's terms. The keys involved are listed under
Basis above.

Expected result: a Data safety section reading "No data collected" and
"No data shared".

## Content rating questionnaire

Questionnaire email: the support address (see Developer contacts).
Category: **Game**.

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

Expected result: **{{EXPECTED_RATING}}** or each region's equivalent. The
rating is not applied until the questionnaire is submitted.

## Target audience and content

- **Age groups:** {{AGE_GROUPS}}
- **Appeals to children by design:** {{APPEALS_TO_CHILDREN}}

{{AUDIENCE_REASON}}

> Draw the reason from the actual art and copy — palette, characters,
> reward loops, language. Selecting any group under 13 brings the
> Families policy, a stricter ads and SDK regime, and a separate content
> review.

## Declarations

| Declaration | Answer |
| --- | --- |
| Contains ads | {{ADS_ANSWER}} |
| Advertising ID | **{{AD_ID_ANSWER}}** — `com.google.android.gms.permission.AD_ID` is not in the manifest |
| Restricted permissions | {{RESTRICTED_PERMISSIONS}} |
| Background location | {{Q_LOCATION}} |
| Health, finance, government, news | {{Q_SPECIAL_CATEGORY}} |
| Data safety resubmission needed | {{Q_RESUBMIT}} |

For restricted permissions, point at the Phase 11 permission dump rather
than asserting it from memory.

## Privacy policy

- **URL:** {{PRIVACY_URL}} — goes in both **Store listing → Privacy
  policy** and **App content → Privacy policy**
- **Source:** `docs/publishing/privacy/index.html` plus `style.css`,
  self-contained, no scripts and no external resources, so any static
  host will serve it
- **Host:** {{PRIVACY_HOST}}

The page must stay reachable after publication. A 404 on the privacy
policy is grounds for removal, not merely rejection — so a host that
hands out a URL without an account does not qualify.

## Developer contacts

- **Support email:** `{{SUPPORT_EMAIL}}`

It is publicly visible on the listing, must match the contact in the
privacy policy, and is best kept distinct from the Console account
address.
