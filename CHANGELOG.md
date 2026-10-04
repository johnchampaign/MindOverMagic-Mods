# Changelog

## Bundle v1.2.1 - 2026-10-03

- Publish a standalone directly extractable ZIP for each module, alongside the
  existing all-modules convenience ZIP. Players can now install exactly the
  modules they choose without deleting unwanted plugin folders.

## Bundle v1.2.0 - 2026-10-03

### Archmage Progression 0.1.0

- Add a separately authored, source-available late-game magic progression
  plugin. It does not reuse or inspect any third-party mod files.
- Add configurable passive benefits based on Pyromancy, Geomancy, Divination,
  Manipulation, Hydrokinesis, and Necromancy skill levels.
- Keep all changes in a standalone BepInEx plugin; no base-game YAML is
  modified.
- Deliberately defer bespoke Adept/Archmage powers and Viturgy conviction
  changes until their native hooks can be tested safely.

## Bundle v1.1.6 - 2026-07-19

### Sacrificial Altar 1.3.6

- Move Sacrificial Rites into the native Tier III X-column directly below
  Darker and Darker, preventing the research screen's dynamic tier boundaries
  from cutting through unrelated topics.
- Correct the user-facing documentation to call Sacrificial Rites Tier III,
  matching the Roman numeral displayed by the game.

## Bundle v1.1.5 - 2026-07-19

### Sacrificial Altar 1.3.5

- Resolve every programmatically assigned research reference through its live
  catalog so Dark Arts, Tier II, Research Bench, Codex tags, and the altar carry
  valid numeric IDs instead of key-only, zero-UID references.
- Attach Sacrificial Rites to the native finalized research graph so it appears
  as a searchable child of Dark Arts.
- Validate the Dark Arts-to-Sacrificial Rites graph edge immediately after
  research finalization and report a specific error if it ever regresses.

## Bundle v1.1.4 - 2026-07-19

### Sacrificial Altar 1.3.4

- Fix the black startup screen caused by attempting to patch an inherited
  research-catalog method as though it were declared on the derived catalog.
- Inject Sacrificial Rites through the verified config-bundle pre-finalization
  hook instead.
- Construct the single Sacrificial Rites definition directly in the live
  research catalog after the temporary catalog failed to discover mod YAML.
- Restore every generic catalog static touched during bundle injection in a
  `finally` block, including on failure, so base-game post-load remains valid.
- Assign the custom research definition's ID before catalog insertion and
  verify/deduplicate it by stored string key rather than a zero-UID `DefId`.
- Roll back all Harmony patches and safely disable the plugin if any future
  patch registration fails, preventing partially patched startup state.

## Bundle v1.1.0 - 2026-07-19

### Sacrificial Altar 1.3.0

- Add a dedicated Tier III **Sacrificial Rites** research topic requiring Dark
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
