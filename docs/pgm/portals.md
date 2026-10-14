# Portals — moving a player who enters a region

A portal moves a player out of a region the moment they enter it: to a point, to a random place in another
region, or by an offset from where they stood. Maps use them for a way home from a score box, a lift between
storeys, a lobby's exits and a respawn room's door. The studio reads, writes and stores the whole
`<portals>` module; this document is the contract, read from `tc.oc.pgm.portals` in `PGMDev/PGM`, and what the
corpus builds.

## 1. The module

`PortalModule.parse` flattens every `<portal>` under a `<portals>` group, nested groups included, and a
group's attributes are every portal's under it, the nearest winning — the cascade `<flags>` and `<wools>`
use. 303 corpus maps write one, 1,720 portals between them.

A portal is triggered by its **region**, entered from outside, or by a `forward` or `transit` filter turning
true; it needs one of the three, and an entrance region beside a `forward` or `transit` filter is refused by
PGM. A `filter` is asked of the player as they would be moved, and `observers` of an observer; both allow by
default.

## 2. Where it sends a player

Five coordinates — `x`, `y`, `z`, `yaw`, `pitch` — each either a place, written `@` and a number, or an offset
from where the player stood, written as a bare number; an unwritten one is an offset of zero. Of the 2,049
corpus coordinates that state `x`, `y` or `z`, 1,434 are places. A `destination` region overrides the
position and lands on a random point inside it, the angles still applied. `bidirectional` sends a player
back through the inverse of the move, `smooth` keeps their momentum, `sound` (true unless stated) plays the
teleport, and `protect` keeps the landing clear of blocks.

## 3. What the studio reads and writes

`MapParser.ParsePortals` reads each into `MapXml.Portals` (`Domain/MapModel.cs`, `Portal`). Every coordinate is
kept as written, `@` included, because which of the two it is decides where the player lands. A region and a
destination are a reference or a child of that name wrapping the shape; the inline one is registered under a
synthetic id and written inline again. Each filter — `filter`, `observers`, `forward`, `reverse`, `transit` —
is an attribute naming one or a child wrapping one, the same way. The writer emits the module flat, with
each portal carrying the attributes its group handed down.

In the map document a portal is `portals[]` (`Serializer.EncodePortal`), and in the database one row of that
JSON in `portal` (migration `M0072`), read back in document order. A map imported before the table existed
holds no portals until it is imported again.

Swept over both corpora, every one of the 225 parseable maps carrying portals writes the same portals back
through the XML and the JSON codec. Two arcade maps name a spawn region the studio does not keep, since it
reads one region per spawn (`PG31`).
