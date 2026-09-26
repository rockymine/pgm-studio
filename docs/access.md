# Who may write

The studio is read by anyone and written by the people it has been told about. That sentence is the whole
policy, and this document is how the code holds it: which request counts as whom, which route needs what, how
a refusal reads, and how the list of people is kept.

## Two modes, and a deployment that forgets to say is closed

`Access:Mode` decides how requests are signed in, and it takes one of two words (`AccessModes`).

**`open`** signs every request in as the *local admin*, an admin with no Minecraft account. It is the mode of a
studio on one person's machine, and of everything that drives one: `appsettings.Development.json` sets it, so
`./tools/dev.sh`, `./tools/e2e.sh` and the drivers in `pgm-studio-mapgen` see the studio exactly as they always
have, and the API test factory sets it for the same reason. A map originated here has no owner, because the
local admin has no account to own it under.

**`invited`** is every other deployment, and it is what an unset `Access:Mode` means — a server whose
configuration never mentions access is closed, not open. A request is signed in by the session cookie
`pgm-studio.session` (HttpOnly, Secure, SameSite=Lax, thirty days sliding), and a request without one is
signed out. SameSite=Lax is also what keeps another site from writing through a visitor's session: a browser
sends the cookie on a cross-site link, and never on a cross-site `POST`, `PUT` or `DELETE`. Signing in with Discord is what writes it (below).

`Access:Admins` lists Minecraft uuids that are admins whatever the whitelist says. It is how the first admin
exists before there is a whitelist to be on, and it keeps the whitelist's keeper from removing themselves out
of it. On a server it is an environment variable, `Access__Admins__0=<uuid>`.

## A caller is an account and a role

A signed-in request carries one claim that matters, the Minecraft uuid (`StudioClaims.Uuid`), and no role. The
role is read from the whitelist on every request (`Callers.OfAsync`), so taking someone off the list, or
changing what they may do, holds on their very next request rather than when their session ends.

What comes out is a `Caller`, which is one of four things: signed out; signed in as an account the whitelist
does not hold; a **member**; or an **admin** (`StudioRoles`). The uuid is the same one an author is credited
under in `map.xml`, which is what lets "may this person change this map" be answered from the map's own
credits.

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

## Which route needs what is decided from the route

No endpoint states its own access. `AccessRules.Apply` runs over every endpoint from the FastEndpoints
configurator in `Program.cs` and decides from the verb and the path:

| Route | Needs | Policy |
|---|---|---|
| `GET`, `HEAD` — any | nobody | open |
| a write under `/map/{slug}` | someone who may edit that map | `map-editor` |
| `DELETE` of anything else | an admin, since a library row is shared by every map using it | `admin` |
| any other write | a person on the whitelist | `member` |

An endpoint that states its own access keeps it: the whitelist's routes are an admin's, list included, and
signing out is anyone's.

**Who may edit a map** is `Callers.MayEditAsync`: an admin; the map's **owner**, the person who originated it
(`map.owner_uuid`, set by `MapOrigin` from the request that brought the row into existence); or someone the map
credits with the role `author`. A `contributor` is credited and may not edit. A `map-editor` route whose slug
names no map passes the gate and answers its own 404, because a map that is not there is not an access
question.

**`POST /api/map/from-documents` is the one write that names its map in the body**, so the rule above cannot
see which map it touches. It asks itself: loading over a stored slug replaces that map, and a caller who may
not edit it is refused 403 before anything is written. A replaced map keeps its owner.

## What it refuses

A request the rules turn away answers the refusal envelope every gate uses (`docs/refusals.md`), written by
`AccessRefusals` rather than by the authentication scheme, so nothing ever redirects to a sign-in page.

| Rule | Status | When |
|---|---|---|
| `RQ7` | 401 | the route writes and the request is signed out |
| `RQ8` | 403 | the request is signed in and this write is not theirs: not on the whitelist, not the map's owner or credited author, or an admin's route — and a Discord sign-in that resolves to nobody on the whitelist |
| `RQ9` | 503 | a sign-in route, on a studio with no Discord application configured |

Every write publishes both in the schema at `/api/openapi/v1.json`, and no read does, so an endpoint table in
`docs/tools/` does not repeat them — they are declared in one place, like the 400 and 500 every route carries.

## The API

| Endpoint | Answers | Fails with |
|---|---|---|
| `GET /api/me` | `{mode, signedIn, uuid, name, role}` — who the request is and what role it carries | — |
| `GET /api/users` | the whitelist, by name: `[{uuid, name, role, addedAt}]` | 403 |
| `POST /api/users` `{player, role}` | puts the account `player` names — a name or a uuid, resolved through Mojang — on the whitelist in `role`, or changes the role of one already on it; answers the stored row | 400, 404 |
| `DELETE /api/users/{uuid}` | takes the person off; they keep every credit and write nothing more | 404 |
| `POST /api/users/{uuid}/invite` | opens an invitation for someone on the whitelist, replacing any open one: `{link, expiresAt}`. The link is shown this once | 404 |
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

## Limits

- **Behind a reverse proxy the callback needs the forwarded scheme.** The handler builds its
  `redirect_uri` from the request it sees, so a server behind Caddy has to honour `X-Forwarded-Proto` or
  Discord is asked to return to `http://`; that is part of `RP78`.
- **A caller without a browser has no way in.** A token for the drivers and agents that write over HTTP is
  `RP76`.
- **The client does not know who it is.** It renders every control to everyone and a write the caller may not
  make comes back as the refusal; reading `GET /api/me` and hiding what it rules out is `RP77`.
- **A read is open, and some reads are expensive.** A world export or a render is a `GET` anyone may send;
  bounding what one caller can ask for at once is `RP79`.
