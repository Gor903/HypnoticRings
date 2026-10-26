# Hosting the privacy policy

Two static files, no scripts, no external resources — any host serves
them. The constraint is not technical: **the URL has to outlive
publication.** A 404 on the privacy policy is grounds for removal, not
merely rejection, so hosts that hand out a URL without an account are
exactly the wrong ones.

## The one step that is the human's

Authentication. Their token must not reach the agent, so they run this
themselves — once, ever:

```sh
npx wrangler login
```

It opens a browser and grants the local CLI a token. Everything after it
is scriptable.

## Deploying

Publishing to the internet is a confirm-first action. Ask before the
first deploy, and again before any redeploy that changes a live URL.

```sh
cd docs/publishing/privacy
npx wrangler deploy
```

The project name comes from `wrangler.jsonc`. Cloudflare now folds Pages
into Workers, so a new project lands on `*.workers.dev` rather than
`*.pages.dev`; for two static files the two are the same thing, and the
surviving product is the safer home for a URL that must never 404.

## Verifying — this is the phase-9 gate

Not "it deployed". Four checks, each a command:

```sh
URL=https://{{PROJECT}}.{{ACCOUNT}}.workers.dev
curl -sS -o /dev/null -w '%{http_code}\n' $URL              # 200
curl -sS -o /dev/null -w '%{http_code}\n' $URL/style.css    # 200
curl -sS $URL | diff - index.html && echo "served bytes match source"
curl -sS -o /dev/null -w '%{http_code} %{redirect_url}\n' http://{{HOST}}   # upgrades to https
```

And for a multi-locale policy, that the language switch works **both**
ways — a one-way link is the half of this that goes unnoticed.

Static hosts commonly strip `.html` and redirect. Relative links between
locale pages still work through the redirect, but the canonical URLs are
the extension-less ones. Record those, and put the canonical one in both
console fields.

## After publication

The project stays. Record in `docs/publishing/README.md` who owns the account
and what would make the URL disappear — an expired card, a deleted
project, a renamed worker. All three have killed a live policy URL
somewhere.
