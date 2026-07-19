# Changelog

## Bundle v1.1.0 - 2026-07-19

### Sacrificial Altar 1.3.0

- Add a dedicated Tier II **Sacrificial Rites** research topic requiring Dark
  Arts and costing 3,000 research.
- Make Sacrificial Rites solely responsible for unlocking the altar instead of
  appending a clipped third reward card to the fixed-width Dark Arts reward row.

## Bundle v1.0.3 - 2026-07-19

### Sacrificial Altar 1.2.5

- Synchronize the altar reward across the research definition passed to the
  selected-topic UI, the static definition catalog, and the live config bundle.
- Log object identity, reward counts, and final archetype resolution at the
  research UI boundary so presentation is verified where it is actually used.

## Bundle v1.0.2 - 2026-07-19

### Sacrificial Altar 1.2.4

- Fix the missing Sacrificial Altar card in the Dark Arts research description.
- Register and validate the altar in the live config bundle catalog used by the
  research UI, rather than relying on a potentially different load-time catalog.
- Initialize every distinct config bundle once so an earlier temporary bundle
  cannot prevent the live research bundle from receiving the reward.

## Bundle v1.0.1 - 2026-07-19

### Sacrificial Altar 1.2.3

- Show the Sacrificial Altar in the Dark Arts research topic's unlock rewards.

## Bundle v1.0.0 - 2026-07-19

First public bundle release.

### Character Level Relics 1.1.0

- Determine manifested relic level caps from mage level and completed trials.
- Remove wand-tier and completed-apprenticeship contributions.
- Preserve the level-24 manifested-relic maximum.

### Faction Balance 1.0.0

- Change Raven Cult Power growth from S to A.
- Change Shattered Power growth from A to S.
- Reduce Raven Cult base HP from 150 to 125 by default.
- Reduce Raven Cult damage bonus from 20 to 10 by default.
- Make the Raven Cult numerical changes configurable.

### Sacrificial Altar 1.2.2

- Add the Sacrificial Altar, its procedural in-world model, build icon, costs,
  construction behavior, and Dark Arts research requirement.
- Add the Sacrifice for Relic ritual for Students, Apprentices, and Staff.
- Preserve and return the selected relic after upgrading its level cap.
- Kill the sacrifice, produce a corpse, and apply school-wide grief for two
  days.
- Add the Dark Temple room, +1 ritual bonus, room requirements, and automatic
  dark room treatment.
- Prevent the procedural prefab template from appearing near the entrance.
- Hide the altar from the build menu until Dark Arts is completed.
