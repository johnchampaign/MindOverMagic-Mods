# Changelog

## Bundle v1.2.2 - 2026-10-06

### Archmage Progression 0.2.0

- Add school rank badges, listed in the selected-mage summary under the
  portrait. Every mage now carries an informational, persistent
  "Adept" or "Archmage" status for each magic school they have trained to the
  configured level (defaults: Adept at 5, Archmage at 8, the base-game cap).
  All seven schools have ranks, including Viturgy (Nature).
- Rank status tooltips state the rank threshold and the exact per-level and
  minimum bonus the plugin grants for that school, filled in from the config
  file at load time so the text always matches the install's settings.
- Earning a rank writes "<mage> earned the title of <rank>" to the log book.
- Ranks are re-evaluated the moment a trained skill changes and on the game's
  own periodic student status sweep, so existing saves pick up badges on load
  without any save edits.
- Rank statuses are purely informational; the per-skill passive bonuses from
  0.1.0 are unchanged and apply whether or not badges are enabled.
- Ship the rank definitions as plugin-owned YAML in a `Content` folder next to
  the DLL; base-game files remain untouched.
- Add Viturgy's per-level Conviction bias (default +2 Conviction target per
  level). It is a native "Viturgy Attunement" status, so the Status &
  Conviction panel lists it with its exact value.
- Add rank powers. Air Adepts walk twice as fast and Air Archmages fly and pass
  through walls. Fire Adepts cook twice as fast and start battles with two
  rounds of Counterattack. Dark Archmages' ordinary needs stop decaying. Each
  Nature Archmage gives every other mage +5 Conviction target.
- Add more rank powers. Earth Adepts start battles with armour equal to half
  their Max HP, and Earth Archmages are immune to harmful combat effects.
  Water Adepts shed harmful combat effects every round. Lightning Archmages
  cast without spending mana. Dark Adepts regain 10% of their spell damage and
  25 HP whenever a foe falls. While the school has a Nature Adept, every room
  gains +2 Luxury.
- Add the Waters of Return. While the school has a Water Archmage, it can build
  a fountain whose ritual raises the mage buried in a grave in the same room.
  The game has no native resurrection, so the mage is rebuilt the way the game
  creates students and staff; badges, likes, relationships and statuses are
  lost. Every check runs before anything changes, and the ritual cannot start
  unless the revival would succeed.
- Add the third group of powers. Lightning Adepts start battles standing on a
  real mana vein. Fire Archmages burn away one of their own traumas or scars
  once a day and are Renewed by Flame. Dark Archmages gather a dark reagent
  bounty at midnight and noon. While the school has an Earth Archmage, it can
  build Nexus Gateways that teleport to one another.
- Add Council powers Rematch Spoils (1: boss rematches pay the first-victory
  rewards, relic included), Open Ledgers (4: scouting reveals every side quest)
  and Swift Travel (each Archmage rank cuts remaining quest travel by a tenth).
- Add Council powers Refining (2: refineries return double), Mending (6:
  wounds close three times as fast) and Steady Minds (7: breaks no longer
  leave mages At Death's Door).
- Add the Archmage Council. Every Archmage rank in the school counts. At 3,
  every mage gains +10 Conviction target; at 5, teaching and learning run
  twice as fast. The founder carries a badge listing the powers in force.
- Fix Necromancy's per-level bonus. Since 0.1.0 it reduced the rate at which
  eating, sleeping and recreation refill needs, a penalty. It now slows need
  decay as described.
- Add a stat breakdown. The HP and Mana bar hovers on the selected-mage panel
  and the Max HP, Max Mana and Damage Bonus hovers on the Mage Sheet now list
  each school's contribution. These UI patches skip themselves with a warning
  if a game update renames their target, rather than disabling the plugin.

### Sacrificial Altar 1.3.7

- Load the plugin's status YAML through the unpatched base catalog loader. The
  previous path called the patched loader on a temporary catalog, which ran
  every other installed plugin's status-catalog postfix against it. With
  Archmage Progression installed that could consume this plugin's once-only
  injection guard and leave the Sacrificed Mage status out of the live catalog.
- Re-inject the Sacrificed Mage status whenever a status catalog lacks it, and
  only ever add this plugin's own definition to the live catalog.
- No gameplay changes.

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
