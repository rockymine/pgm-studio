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
sends the cookie on a cross-site link, and never on a cross-site `POST`, `PUT` or `DELETE`. Nothing writes that cookie yet: the sign-in that does is `RP75`, so an invited studio today is
read-only for everyone.

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

## Which route needs what is decided from the route

No endpoint states its own access. `AccessRules.Apply` runs over every endpoint from the FastEndpoints
configurator in `Program.cs` and decides from the verb and the path:

| Route | Needs | Policy |
|---|---|---|
| `GET`, `HEAD` — any | nobody | open |
| a write under `/map/{slug}` | someone who may edit that map | `map-editor` |
| `DELETE` of anything else | an admin, since a library row is shared by every map using it | `admin` |
| any other write | a person on the whitelist | `member` |

An endpoint that names a policy itself keeps it; the whitelist's own routes are the only ones that do, so that
reading the list is an admin's alone.

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
| `RQ8` | 403 | the request is signed in and this write is not theirs: not on the whitelist, not the map's owner or credited author, or an admin's route |

Every write publishes both in the schema at `/api/openapi/v1.json`, and no read does, so an endpoint table in
`docs/tools/` does not repeat them — they are declared in one place, like the 400 and 500 every route carries.

## The API

| Endpoint | Answers | Fails with |
|---|---|---|
| `GET /api/me` | `{mode, signedIn, uuid, name, role}` — who the request is and what role it carries | — |
| `GET /api/users` | the whitelist, by name: `[{uuid, name, role, addedAt}]` | 403 |
| `POST /api/users` `{player, role}` | puts the account `player` names — a name or a uuid, resolved through Mojang — on the whitelist in `role`, or changes the role of one already on it; answers the stored row | 400, 404 |
| `DELETE /api/users/{uuid}` | takes the person off; they keep every credit and write nothing more | 404 |

The three whitelist routes are an admin's and answer 401 or 403 to anyone else.

## Driving it without the UI

In an open studio every request is the local admin, so nothing below needs a session:

```bash
curl -s localhost:7894/api/me
# {"mode":"open","signedIn":true,"uuid":null,"name":"local","role":"admin"}

curl -s -X POST localhost:7894/api/users -H 'content-type: application/json' \
     -d '{"player":"Notch","role":"member"}'
# {"uuid":"069a79f4-44e9-4726-a5be-fca90e38aaf5","name":"Notch","role":"member","addedAt":"…"}

curl -s localhost:7894/api/users
curl -s -X DELETE localhost:7894/api/users/069a79f4-44e9-4726-a5be-fca90e38aaf5
```

The `POST` needs Mojang to answer for a name it has not seen; a container with no egress refuses it 404, and
the uuid of an account the studio has already resolved goes through.

## Limits

- **Nothing signs a person in yet.** The Discord sign-in that writes the session, and binds a Discord account
  to a whitelisted Minecraft uuid, is `RP75`; until it lands an invited studio is read-only.
- **A caller without a browser has no way in.** A token for the drivers and agents that write over HTTP is
  `RP76`.
- **The client does not know who it is.** It renders every control to everyone and a write the caller may not
  make comes back as the refusal; reading `GET /api/me` and hiding what it rules out is `RP77`.
- **A read is open, and some reads are expensive.** A world export or a render is a `GET` anyone may send;
  bounding what one caller can ask for at once is `RP79`.
