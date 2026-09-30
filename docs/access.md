# Who may write

The studio is read by anyone and written by the people it has been told about. That sentence is the whole
policy, and this document is how the code holds it: which request counts as whom, which route needs what, how
a refusal reads, and how the list of people is kept.

## Two modes, and a deployment that forgets to say is closed

`Access:Mode` decides how requests are signed in, and it takes one of two words (`AccessModes`).

**`open`** signs every request in as the *local admin*, an admin with no Minecraft account. It is the mode of a
studio on one person's machine, and of everything that drives one: `appsettings.Development.json` sets it, so
`./tools/dev.sh`, `./tools/e2e.sh` and the drivers in `pgm-studio-mapgen` see the studio exactly as they always
have, and the API test factory sets it for the same reason. A map originated here has no owner and credits
nobody, because the local admin has no account to own it under.

**`invited`** is every other deployment, and it is what an unset `Access:Mode` means — a server whose
configuration never mentions access is closed, not open. A request is signed in by the session cookie
`pgm-studio.session` (HttpOnly, Secure, SameSite=Lax, thirty days sliding), and a request without one is
signed out. SameSite=Lax is also what keeps another site from writing through a visitor's session: a browser
sends the cookie on a cross-site link, and never on a cross-site `POST`, `PUT` or `DELETE`. Signing in with Discord is what writes it (below).

`Access:Admins` lists Minecraft uuids that are admins whatever the whitelist says, and it names the studio's
**owners**. It is how the first admin exists before there is a whitelist to be on, and it is the one grant the
studio cannot make or undo itself: only the server's configuration changes it. On a server it is an
environment variable, `Access__Admins__0=<uuid>`.

## A caller is an account and a role

A signed-in request carries one claim that matters, the Minecraft uuid (`StudioClaims.Uuid`), and no role. The
role is read from the whitelist on every request (`Callers.OfAsync`), so taking someone off the list, or
changing what they may do, holds on their very next request rather than when their session ends.

What comes out is a `Caller`, which is one of four things: signed out; signed in as an account the whitelist
does not hold; a **member**; or an **admin** (`StudioRoles`). An admin may also be an **owner**
(`Caller.IsOwner`): a uuid `Access:Admins` names, signed in from a browser, or an open studio's local admin.
Owner is not a role on the whitelist, since nothing the studio stores can grant it. The uuid is the same one an author is credited
under in `map.xml`, which is what lets "may this person change this map" be answered from the map's own
credits.

## Who keeps the whitelist

**An admin keeps the members; only an owner makes or unmakes an admin.** Every change to someone's place on
the whitelist goes through one rule, `WhitelistKeeping`, and a change it turns away is refused `RQ8` at 403
before anything is written.

| Change | An admin | An owner |
|---|---|---|
| add a member, or change a member's role to member | yes | yes |
| make someone an admin | no | yes |
| change, remove, invite, or issue a token for an admin | no | yes |
| open an invitation for someone a Discord account already signs in as | no | yes |
| anything to an owner | no | only to themselves |

**Redirecting an account is an owner's, because an invitation binds whoever follows it.** Following one binds
the Discord account that signs in through it to that person and unbinds any account bound before, so an
invitation opened for someone who already signs in hands their account to whoever follows the link. An admin
opens one for a member nobody signs in as yet; an owner opens one for anyone but another owner, and for
themselves, which is how an owner who lost their Discord account binds a new one.

**An owner is changed only by the server.** No route demotes, removes, invites or issues a token for a uuid
`Access:Admins` names, except that owner in their own browser. The owner's own recovery is on the server too:
`tools/deploy/invite.sh` runs a studio in open mode on the machine, whose local admin is an owner, and prints a
fresh invitation (`docs/deployment.md`).

**A token is never an owner.** It is capped at a member's rights (below), so an owner's token keeps no
whitelist at all.

## Signing in: Discord says who, an invitation says which account

Discord answers who a person is on Discord and nothing else, so it cannot say which Minecraft account they
play. That comes from an **invitation**: an admin puts the person on the whitelist, opens an invitation for
them, and hands them the link it answers. The first Discord account that signs in through the link is bound to
that person (`studio_user.discord_id`), and the invitation closes. From then on the plain sign-in finds the
Minecraft account from the Discord account alone. A Discord account signs in as one person, so following a
second invitation moves it there. An invitation stays open for seven days (`DiscordSignIn.InviteLifetime`), a
new one replaces any still open, and the whitelist stores only the SHA-256 of its code, so the table never
holds a link that works.

The sign-in itself is OAuth2's authorization-code flow with PKCE, over ASP.NET's own OAuth handler — no
library beyond the framework. `GET /api/auth/discord` (or the invitation) sends the browser to Discord asking
for the `identify` scope alone: an id and a username, no e-mail and no servers. Discord sends it back to
`/api/auth/discord/callback`, which the handler answers by exchanging the code for a token with the
application's secret, server to server, and reading `/users/@me`. What Discord said lands in a five-minute
cookie of its own, and `GET /api/auth/discord/complete` decides who that is (`DiscordSignIn.ResolveAsync`):
the session is written only when the answer is someone on the whitelist, and then the browser goes on to
`returnUrl` — a path on this studio, never another site.

The Discord application is two settings. `Discord:ClientId` is public — it is in every sign-in link — and
`appsettings.json` carries it; its redirect list names `…/api/auth/discord/callback` for every host the studio
answers on. `Discord:ClientSecret` is the application's password and lives only in the server's environment
(`Discord__ClientSecret`) or, on a developer's machine, in user secrets. A studio missing either refuses every
sign-in route `RQ9` at 503.

**The first invitation is opened with the studio `open`.** An admin issues invitations, and an admin signs in
through one — so the first is made where every request is already the admin. Both modes read the same
database, so what the open studio writes is what the invited one reads. On a developer's machine:

```bash
dotnet user-secrets set Discord:ClientSecret '<secret>' --project src/PgmStudio.Api

./tools/dev.sh restart                                   # open: every request is the local admin
curl -s -X POST localhost:7894/api/users -H 'content-type: application/json' \
     -d '{"player":"<your name>","role":"admin"}'
curl -s -X POST localhost:7894/api/users/<your uuid>/invite    # → {"link": …}

Access__Mode=invited ./tools/dev.sh restart              # now closed
# open the link in a browser, sign in with Discord, then http://localhost:7894/api/me names you
```

## A token for a caller without a browser

**A driver or an agent signs in with a token instead of a browser.** It sends
`Authorization: Bearer pgms_…`, and the access scheme hands any request naming a token to the `token` scheme
(`TokenAccessHandler`) rather than to the session cookie. A token signs in as the person it was issued for —
their uuid and nothing else, the same as a session — so the whitelist decides what it may do on every
request: the same maps, and the same credit on a map it originates.

**A token never carries more than a member's rights, and never issues a token.** An admin's token is a
member's: it keeps no whitelist, opens no invitation and changes no map its person does not own
(`Callers`), since a token lives in an environment a browser session does not, and one that leaks there
should not hand over the studio. And a request signed in by a token is refused `RQ8` when it asks for a token
of its own or anyone's, so a leaked token cannot outlive its revocation by issuing another. Both are asked
from a browser.

**The notes permission is the one thing a token lifts the cap for.** Map notes — an admin's feedback pinned
to a board, and an agent's answers to it (`docs/tools/sketch.md`, *Notes*) — are read and written by an admin,
so an agent answering them needs an admin's reach for exactly that and nothing else. A token issued with
`notes` carries it (`studio_token.notes`), and it holds only while the token's person is an admin: a person
taken down to member keeps their token and loses its notes on the very next request, since the role is read
from the whitelist every time. It is asked for when the token is issued, and refused `RQ8` on a member's
token, so no token ever exceeds its person. A leaked notes token still cannot keep the whitelist, issue an
invitation or change a map its person does not own.

**A token is issued once and kept only as a hash.** Its person issues one from *Tokens* in the account menu or
`POST /api/users/me/tokens`, and an admin issues one for a member (an owner for anyone but another owner) with
`POST /api/users/{uuid}/tokens`. The answer is the only one that carries the token; `studio_token` keeps its
SHA-256 beside the person it acts as, a label saying what it is for, when it was issued and when it last
signed a request in, to the minute. The secret is made the way an invitation's code is (`StudioSecret`), with
`pgms_` in front so a token is recognisable wherever it is pasted.

**A token ends when it is revoked or its person leaves the whitelist.** `DELETE /api/users/me/tokens/{id}`
revokes one of the caller's own, and taking a person off the whitelist revokes every token that acts as them.
A token the studio does not hold fails the sign-in outright, so a write carrying one is refused `RQ7` rather
than read as a visitor's. A token does not lapse on its own: it lives until one of the two.

**It is a secret of the environment that drives, never of a prompt.** A Claude Code cloud session holds it
as its environment's **API credential** for `pgmstudio.de`: the session's proxy adds `Authorization: Bearer`
to every request for that host after it leaves the session, so a plain `curl` is signed in and the token is
never in the session to leak. Off the cloud it is `PGM_STUDIO_TOKEN`, which the pgm-studio-mapgen tools send on
every request, and only over https or to the same machine.

**The studio's address is the chat's, not the environment's.** Whether a session drives the deployed studio
or sets up its own is the author's decision, taken per chat (`pgm-studio-mapgen/CLAUDE.md`), so
`PGM_STUDIO_API` is set by the session that was asked for the deployed one. An open studio needs no token —
every request is already its admin — and `POST /api/users/me/tokens` answers the local admin 404, since there
is no account for a token to act as.

## What the browser shows

The studio's bar says who the browser is on every page: the head, name and role signed in, opening a menu to
sign out; *Sign in with Discord* for a visitor; `local` in an open studio. For an admin it also carries *Users*,
the whitelist at `/admin/users`. That page
adds a player by name or uuid in a role, changes a role, takes someone off, and opens an invitation whose link
it shows once with a copy button — the same four routes as below. It tags an owner, and greys each control
the rule above closes to the caller, with the reason on hover: an admin who is no owner sees no *admin* to give
and no control on an admin's or an owner's row, and no invitation for someone who already signs in.

Someone on the whitelist also finds *Tokens* in the account menu, at `/tokens`: it issues a token and shows it
once with a copy button, and lists theirs with what each is for and when it was last used, to revoke. An admin
sees one more box there, *May read and answer map notes*, and a token issued with it is marked `notes` in the
list.

A page the caller may not write opens read-only: the tool's bar says *View only*, with the reason on hover, the
panels grey their fields and the canvas keeps only the tools that look. Signed in, a read-only Sketch page
still draws the ground's relief, its paint and the 3-D preview; signed out, it draws the outlines alone. `docs/client/ui-conventions.md` says
how the shell decides it. `tests/e2e/access.mjs` holds it, against a second server over the suite's database
running invited with the browser signed out.

## Which route needs what is decided from the route

No endpoint states its own access. `AccessRules.Apply` runs over every endpoint from the FastEndpoints
configurator in `Program.cs` and decides from the verb and the path:

| Route | Needs | Policy |
|---|---|---|
| `GET`, `HEAD` — any | nobody | open |
| a `POST` that only reads, marked `[PostedRead]` | someone signed in | `member` |
| a write under `/map/{slug}` | someone who may edit that map | `map-editor` |
| `DELETE` of anything else | an admin, since a library row is shared by every map using it | `admin` |
| any other write | a person on the whitelist | `member` |
| a map-notes route, reads included — `/notes`, `/map/{slug}/notes` | an admin, or a token carrying the notes permission | `notes` |

An endpoint that states its own access keeps it: the whitelist's routes are an admin's, list included,
revoking one's own token is a member's, the notes routes are the `notes` policy's, reads included, and signing
out is anyone's.

**A read that carries a body is a `POST`, and it is still a read.** The Sketch page draws its paint, its relief
contours and its 3-D world from the live layout, which it posts to `sketch/paint`, `sketch/relief` and
`sketch/columns`; `sketch/relief/read`, `sketch/dressing`, `sketch/seats` and `sketch/probe-footprint` answer
the same way. Each computes an answer and stores nothing, and says so with `[PostedRead]`, which gives it the
`member` policy whichever map it names: anyone signed in sees how a map they may not change is made. A
visitor who is not signed in is refused them with `RQ7`, because each answer is a build — `sketch/columns`
builds the whole world — and it waits its turn like every build (below).

**Who may edit a map** is `Callers.MayEditAsync`: an admin; the map's **owner**, the person who originated it
(`map.owner_uuid`, set by `MapOrigin` from the request that brought the row into existence); or someone the map
credits with the role `author`. A `contributor` is credited and may not edit.

**Whoever originates a map is also credited with it.** `MapOrigin` writes the originator's uuid and Minecraft
name as the map's first `author` row in the same transaction as the map itself, so a new sketch or plan names
its maker without them typing themselves in. The credit and the ownership are two things: removing oneself
from the authors takes one's name off the `map.xml` and leaves the map one's own to edit. An intent written
before anyone was named in it carries the map's credits into its `meta` (`IntentWrite`), since the export
reads the intent and would otherwise say the map is by nobody (`EX6`). A `map-editor` route whose slug
names no map passes the gate and answers its own 404, because a map that is not there is not an access
question.

**`POST /api/map/from-documents` is the one write that names its map in the body**, so the rule above cannot
see which map it touches. It asks itself: loading over a stored slug replaces that map, and a caller who may
not edit it is refused 403 before anything is written. A replaced map keeps its owner.

## What the studio builds at once

**A request that builds a world waits its turn.** A world export, `map.xml`, every render and every read
measured off the built world — `reach`, `incline`, `slopes`, `column`, `walk`, `transect`, `stroke`,
`themes/census`, `views`, `coverage` — the posts that build one (`sketch/columns`, `sketch/dressing`,
`sketch/seats`, `plan/columns`) are marked `[Queued]`. Each costs seconds of CPU and a share of memory on a
machine every caller shares: a cold export takes 3–12 s on a two-core server. `BuildQueue` holds such a request until the studio has
a turn free and the caller has one of their own, and gives both back when the response is written. Every other
route never waits.

**A caller runs one build at a time, and waits behind their own.** The studio runs three queued requests at
once and one caller one of them, so a caller who sends eight exports gets them one after another while another
caller's first export takes the next free turn. The caller is the account a signed-in request acts as, a
token's included; a visitor is their address, which behind a reverse proxy is the forwarded one; every request
to an open studio is its one local admin.

**A request that cannot wait is refused `RQ11` at 429.** The queue holds 32 waiting requests, 8 of them one
caller's, and a request waits 60 s for its turn; past any of those it answers the refusal envelope with
`Retry-After: 10`, before its handler runs. Access is decided first, so a request the rules refuse never takes
a turn. Every queued route publishes the 429 in the schema, and like the 401 and 403 no endpoint table in
`docs/tools/` repeats it.

The bounds are the operator's, under `Builds` in `appsettings.json`:

| Setting | Default | Bounds |
|---|---|---|
| `Builds:Slots` | 3 | queued requests running at once on the studio |
| `Builds:PerCaller` | 1 | of those, one caller's — 3 in `Development`, where every request is the local admin |
| `Builds:Waiting` | 32 | requests waiting for a turn |
| `Builds:WaitingPerCaller` | 8 | of those, one caller's |
| `Builds:MaxWaitSeconds` | 60 | how long a request waits before it is refused |

**The thread pool starts at sixteen threads.** A build runs synchronously on a pool thread, and the pool's
floor is the machine's core count, so on two cores two builds left nothing to answer the cheap routes while the
pool grew: pinned to two cores, `/api/health` took 1.1 s behind two renders and 3.3 s behind two exports.
`ThreadPoolMinThreads` in `PgmStudio.Api.csproj` raises the floor to 16, and the same measurements answer in at
most 40 ms, 50 ms with four builds running; the builds take as long as they did.

## What it refuses

A request the rules turn away answers the refusal envelope every gate uses (`docs/refusals.md`), written by
`AccessRefusals` rather than by the authentication scheme, so nothing ever redirects to a sign-in page.

| Rule | Status | When |
|---|---|---|
| `RQ7` | 401 | the route writes, builds a view on request, or reads map notes, and the request is signed out |
| `RQ8` | 403 | the request is signed in and this write is not theirs: not on the whitelist, not the map's owner or credited author, or an admin's route — and a Discord sign-in that resolves to nobody on the whitelist |
| `RQ9` | 503 | a sign-in route, on a studio with no Discord application configured |
| `RQ11` | 429 | a route that builds a world, and the build queue is full or the request waited past its limit |

Every write publishes the first two in the schema at `/api/openapi/v1.json`, and no read does, so an endpoint table in
`docs/tools/` does not repeat them — they are declared in one place, like the 400 and 500 every route carries.

## The API

| Endpoint | Answers | Fails with |
|---|---|---|
| `GET /api/me` | `{mode, signedIn, uuid, name, role, notes, owner}` — who the request is, what role it carries, whether it may read and answer map notes, and whether it is an owner | — |
| `GET /api/map/{slug}/access` | `{mayEdit}` — whether this request's writes to the map would be accepted; the client opens it read-only where not | 404 |
| `GET /api/users` | the whitelist, by name: `[{uuid, name, role, addedAt, signsIn, inviteExpiresAt, owner}]` — `signsIn` once a Discord account is bound, `inviteExpiresAt` while an invitation is open, `owner` where `Access:Admins` names them | 403 |
| `POST /api/users` `{player, role}` | puts the account `player` names — a name or a uuid, resolved through Mojang — on the whitelist in `role`, or changes the role of one already on it; answers the stored row | 400, 403 `RQ8` (an admin made or changed by a non-owner, or an owner changed), 404 |
| `DELETE /api/users/{uuid}` | takes the person off; they keep every credit and write nothing more | 403 `RQ8` (an admin, by a non-owner; an owner) · 404 |
| `POST /api/users/{uuid}/invite` | opens an invitation for someone on the whitelist, replacing any open one: `{link, expiresAt}`. The link is shown this once | 403 `RQ8` (an admin, someone who already signs in, by a non-owner; an owner) · 404 |
| `GET /api/users/me/tokens` | the caller's tokens, newest first: `[{id, label, issuedAt, lastUsedAt, notes}]`; empty for a visitor or an open studio's admin | — |
| `POST /api/users/me/tokens` `{label, notes?}` | issues a token acting as the caller: `{id, label, token, actsAs, notes}`. The token is shown this once; a blank label is `token`; `notes` asks for the notes permission, which only an admin's token carries | 403 `RQ8` (notes on a member's token) · 404 (no account) |
| `DELETE /api/users/me/tokens/{id}` | revokes one of the caller's tokens | 404 |
| `POST /api/users/{uuid}/tokens` `{label, notes?}` | issues a token acting as someone on the whitelist; admin only, and acting as an admin an owner's alone. `notes` is refused where that person is not an admin | 403 `RQ8` · 404 |
| `GET /api/auth/discord?returnUrl=` | 302 to Discord, to sign in with an account already bound | 503 |
| `GET /api/auth/invite/{code}` | 302 to Discord, binding the account that signs in to the invitation's person | 404, 503 |
| `GET /api/auth/discord/complete` | where the sign-in lands: writes the session and 302s to `returnUrl` | 401, 403 |
| `POST /api/auth/sign-out` | ends the session; open to anyone | — |

The whitelist routes are an admin's and answer 401 or 403 to anyone else.

## Driving it without the UI

In an open studio every request is the local admin, so nothing below needs a session:

```bash
curl -s localhost:7894/api/me
# {"mode":"open","signedIn":true,"uuid":null,"name":"local","role":"admin"}

curl -s -X POST localhost:7894/api/users -H 'content-type: application/json' \
     -d '{"player":"Notch","role":"member"}'
# {"uuid":"069a79f4-44e9-4726-a5be-fca90e38aaf5","name":"Notch","role":"member","addedAt":"…"}

curl -s -X POST localhost:7894/api/users/069a79f4-44e9-4726-a5be-fca90e38aaf5/invite
# {"link":"http://localhost:7894/api/auth/invite/…","expiresAt":"…"}

curl -s localhost:7894/api/users
curl -s -X DELETE localhost:7894/api/users/069a79f4-44e9-4726-a5be-fca90e38aaf5
```

The `POST` needs Mojang to answer for a name it has not seen; a container with no egress refuses it 404, and
the uuid of an account the studio has already resolved goes through.

An invited studio is driven with a token, issued once from a browser that is signed in:

```bash
export PGM_STUDIO_API=https://pgmstudio.de/api PGM_STUDIO_TOKEN=pgms_…
curl -s "$PGM_STUDIO_API/me" -H "Authorization: Bearer $PGM_STUDIO_TOKEN"
# {"mode":"invited","signedIn":true,"uuid":"…","name":"…","role":"member"}
curl -s -X POST "$PGM_STUDIO_API/sketch" -H "Authorization: Bearer $PGM_STUDIO_TOKEN" \
     -H 'content-type: application/json' -d '{"name":"Weirgate"}'
```

In a cloud session whose environment holds the token as its API credential, the same reads carry no header:
`curl -s https://pgmstudio.de/api/me` answers signed in.

## Limits

The handler builds Discord's `redirect_uri` from the request it sees, so a studio behind a proxy has to
honour `X-Forwarded-Proto`; the deployed one does, and `docs/deployment.md` says how.


- **A read-only page still lets a few edits start.** The panels grey their fields and the dock drops its
  drawing tools (`docs/client/ui-conventions.md`), but a sidebar's own inputs, a select-and-drag on the canvas
  and a phase bar's finish are not reached; each is refused by the server and springs back. Closing them is
  `RP81`.
- **A world already built still waits its turn.** The queue does not know that a request would be answered
  from the studio's store of built worlds in milliseconds, so a caller's second read of the same board waits
  behind their first.
