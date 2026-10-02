# The studio on a server

pgmstudio.de is one Hetzner Cloud VM running the API, its database and a reverse proxy, and deploying itself
from `main`. This document is that machine: what runs on it, how a commit reaches it, how people get in, and
what is still missing. Who may do what once they are in is `access.md`; how the studio bounds what one
caller asks for is `access.md`'s build queue.

## What runs where

**Everything runs on one machine, and only Caddy faces the internet.** The VM is 2 vCPU and 3.8 GB of memory
with no swap, running Ubuntu 26.04; the firewall (`ufw`) allows 22, 80 and 443 and nothing else.

| Piece | Is | Listens | Configured by |
|---|---|---|---|
| Caddy | TLS for `pgmstudio.de` (Let's Encrypt, renewed by Caddy), `www.` redirected to the apex | `:80`, `:443` | `/etc/caddy/Caddyfile` |
| `pgm-studio.service` | the API and the hosted Blazor client, as the `pgm-studio` user | `127.0.0.1:7894` | `/etc/pgm-studio/pgm-studio.env` |
| MariaDB | the `pgm_studio` database, user `pgm` | `127.0.0.1:3306` | Ubuntu's package defaults |
| `pgm-studio-deploy.timer` | deploys a new green `main` | — | `/opt/pgm-studio/bin/autodeploy.sh` |

`tools/deploy/` holds the copies the server was set up from: the unit, the Caddyfile, an example
environment, the timer and the scripts. The scripts are installed to `/opt/pgm-studio/bin` and every deploy
refreshes them from the commit it deploys; the unit, the Caddyfile and the environment are the server's own
and change only by hand.

**The studio keeps its state in three places outside a release.** The database; `/var/lib/pgm-studio`, the
service user's home, which holds the data-protection keys a sign-in cookie is sealed with (lose them and
every session ends), the Minecraft texture jar, and the pictures map notes carry, under `Notes__Pictures`
(`/var/lib/pgm-studio/pictures`); and `/etc/pgm-studio/pgm-studio.env`. A release under
`/opt/pgm-studio/releases/<commit>` holds nothing that is not rebuilt from the commit.

## The environment

**Every setting the server needs beyond `appsettings.json` is in `/etc/pgm-studio/pgm-studio.env`**, owned
`root:pgm-studio` at mode 640, which the unit reads as its `EnvironmentFile`. `tools/deploy/pgm-studio.env.example`
lists them. A value holding `;` is quoted, because the deploy script sources the file in a shell to run the
migrator, and an unquoted connection string ends at its first `;`.

| Setting | Why the server needs it |
|---|---|
| `ASPNETCORE_ENVIRONMENT=Production` | user secrets, the Development `appsettings` and the static-web-assets manifest are off |
| `ASPNETCORE_URLS=http://127.0.0.1:7894` | only Caddy reaches the API |
| `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` | ASP.NET Core applies `X-Forwarded-Proto` and `-For` itself, so Discord is asked to return to `https://` and a signed-out caller is queued on their own address rather than Caddy's |
| `ConnectionStrings__PgmStudio` | the database; the API and the migrator resolve the same one |
| `Access__Mode=invited`, `Access__Admins__0` | closed to anyone not invited; the owners, by Minecraft uuid — the admins who alone make and unmake admins, and whom only this file changes |
| `Discord__ClientSecret` | the sign-in; the application's redirect list names `https://pgmstudio.de/api/auth/discord/callback` |
| `Textures__AcceptMojangEula=true`, `Textures__Cache` | the eye view's block sprites, downloaded once from Mojang; the cache is set because the default resolves to nothing for a service user whose `~/.local/share` does not exist |
| `Notes__Agent__Fire`, `Notes__Agent__Token` | the agent the author hands notes to: the `/fire` URL and token of a Claude Code Routine's API trigger, both copied from the Routine's edit form. Absent, In game offers no hand-off. The token starts a session on its owner's Claude account, so it lives here and nowhere else (`docs/tools/sketch.md`, *Handing the notes to an agent*) |

**The forwarded headers cannot be forged through Caddy.** Caddy has no `trusted_proxies`, so it replaces
whatever `X-Forwarded-For` a client sends with the address it sees, and the API listens on loopback only.

**Program.cs adds environment variables after the command line**, so a setting in the environment file
cannot be overridden by an argument. A temporary second studio overrides it with a second `EnvironmentFile`,
which systemd reads after the first — the way `invite.sh` does.

## How a commit reaches the server

**A merge to `main` is live about five minutes after its CI passes, with nobody at the machine.**
`pgm-studio-deploy.timer` runs `autodeploy.sh` every five minutes. It reads `main`'s head from GitHub and
stops at once when that commit is the one deployed. Otherwise it asks GitHub for the commit's check runs —
unauthenticated, since the repository is public — and runs `deploy.sh` only when `checks` and
`database-and-browser` have both completed successfully and no run has failed. A run still going waits for the
next tick; a failed one is recorded and skipped.

**A deploy works in its own clone, `/opt/pgm-studio/repo`.** It checks commits out there and nowhere else,
so a checkout someone is working in on the same machine is never moved under them.

**`deploy.sh` is the whole deploy, and running it by hand does the same thing.**
`sudo /opt/pgm-studio/bin/deploy.sh [ref]` (default `origin/main`) runs these steps and stops at the first
that fails:

1. fetch and check out the commit;
2. publish the API and the migrator into `releases/<commit>`, at low priority because the live studio shares
   the two cores, and stop the build servers the publish leaves behind (~800 MB idle on a box with no swap);
3. dump the database to `/var/backups/pgm-studio/pgm_studio-<time>-before-<commit>.sql.gz`, and copy the
   note pictures it does not hold yet into `/var/backups/pgm-studio/pictures` — a picture is named by its hash
   and never changes, so the copy only adds, and a picture does not compress, so it rides in no dump;
4. run `--migrate-only`;
5. point `/opt/pgm-studio/app` at the release and restart the service;
6. wait up to 30 s for `/api/health`, and check the served `index.html` carries no unfilled placeholder;
7. start the Generator's board library fill for this release's composer in the background (below);
8. keep the three newest releases, install the commit's `tools/deploy/*.sh` into `/opt/pgm-studio/bin`, and
   record the commit as deployed in `/var/lib/pgm-studio-deploy/deployed`.

**The board library is composed on the server, after a deploy and behind everything else.** Step 7 starts
`PgmStudio.Import --compose-library` from the new release as the transient unit `pgm-studio-library`, as the
`pgm-studio` user with the studio's environment, at `Nice=19`, a tenth of the default CPU weight and idle I/O,
so it outlives the deploy and the live studio always has the cores first. It composes only what the running
composer version is missing, so a deploy that does not change the composer finishes it at once, and a fill left
running by the release before is stopped first. `journalctl -u pgm-studio-library` shows it band by band, and
`docs/tools/generator.md` is what it writes.

**A studio that does not come up is pointed back at the release before it.** The schema stays migrated, since
a migration is not undone by pointing at older code; the dump from step 3 is what restores it. A lock in
`/var/lib/pgm-studio-deploy` keeps a deploy by hand and the timer's from running at once.

**A failed deploy is not retried until something changes.** Its commit is written to
`/var/lib/pgm-studio-deploy/failed`, and the timer skips it; the next merge, or `deploy.sh` run by hand after
the fix, clears it. `journalctl -u pgm-studio-deploy` is every tick's account, and
`systemctl list-timers pgm-studio-deploy.timer` says when the next one is.

**What the deploy cannot do on its own is a step no commit states.** A change that needs a new setting
comes up unhealthy and is rolled back until the setting is in the environment file. Data the studio does not seed itself is the other case. The library seeds itself at every start from the
folder committed in `src/PgmStudio.Minecraft/Library`, the copied trees included, so a change to the seed — a
re-cut of the showcase by `tools/seed-trees.cs`, a house, a pattern, an entry taken out — reaches the server
with the commit and needs no step of its own. The commit is also the only way a seeded row changes: the studio
refuses to edit one (`docs/tools/library.md`, *The seed*).

`tools/deploy/install.sh` installs the scripts, the timer and its service, and records the release the server
already runs as deployed, so its first tick deploys only a `main` that has moved past it.

## Letting people in

**A person is added from `/admin/users` by an admin, or from the server by `invite.sh`.**
`sudo /opt/pgm-studio/bin/invite.sh <minecraft-name> [member|admin]` starts a second studio in open mode on
`127.0.0.1:7896` for at most 60 s, puts the player on the whitelist, prints their invitation link and stops
it. It exists because the whitelist is an admin's and a token is never more than a member's
(`access.md`), and it is how the first admin was made: `Access__Admins__0` makes the uuid an owner, and the
invitation is what binds their Discord account to it. Its local admin is an owner, so it is also an owner's way
back in: run for the owner's own name, it opens an invitation that binds whichever Discord account follows it.

**An agent drives the studio with a token issued from *Tokens* in the account menu**, and `PGM_STUDIO_API`
and `PGM_STUDIO_TOKEN` in its environment (`access.md`). The whitelist holds Minecraft accounts only, so an
agent's token acts as the person who issued it.

## Limits

- **Backups stay on the machine.** The dumps and the picture copies in `/var/backups/pgm-studio` are taken
  before each deploy, on the disk the database is on, and nothing takes one nightly; a lost disk loses both. Off-machine dumps are
  `RP84`.
- **A failed deploy tells nobody.** It is in the journal and in `/var/lib/pgm-studio-deploy/failed`, and the
  site keeps running the release before it, but no message goes out; also `RP84`.
- **One machine is one point of failure.** A deploy restarts the studio, and a request in flight during the
  few seconds it takes is lost.
