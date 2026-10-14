# Damage — the filters every hit is asked

The `<damage>` module decides which hits land. Each child of a `<damage>` element is a filter, asked of every
hit in order: the first that answers allow or deny decides it, and a hit none of them answers lands as it would
without the module. Maps use it to make a fall harmless, a lobby safe, or a team unable to hurt itself. 111
corpus maps write one. This document is the contract, read from `tc.oc.pgm.damage` in `PGMDev/PGM`.

## 1. The module

`DamageModule.parse` reads every `<damage>` element of the root, and every child of each is a filter, required:
a `<deny>` or an `<allow>` around the question, a reference `<filter id="…"/>` to a named one, or any filter
element at all (`DamageMatchModule.queryRules` asks them in order and the first that does not abstain decides).
Of the corpus's 121 filters, 110 are `<deny>`, which is what the module is used for — a hit that matches is not
dealt. An `attacker-action` and a `victim-action` name an action run on either side of a hit; the corpus states
eleven.

## 2. What the studio reads and writes

`MapParser.ParseDamage` reads the module into `MapXml.Damage` (`Domain/MapModel.cs`, `DamageConfig`): the
filters of every element as one list in document order, each registered under a synthetic id where it was
written inline, and the last action each side states. The writer emits one `<damage>` element, each filter
inline where the map wrote it so and a reference where it named one.

In the map document the module is `damage` (`Serializer.EncodeDamage`), and in the database one row of that
JSON in `damage` (migration `M0073`). A map imported before the table existed holds no damage module until it is
imported again.

Swept over both corpora, every one of the 106 parseable maps carrying the module writes it back unchanged through
the XML and the JSON codec. The actions are kept by id; the `<actions>` module they name is not read.
