# Modding beta feedback (draft)

Context: I ported four existing BepInEx mods to the beta in a day: Faction
Balance (YAML only), Character Level Relics (one Harmony prefix), Sacrificial
Altar (new furniture, research, ritual, room type, status, runtime-built
model) and Archmage Progression (92 statuses, two buildings, 33 Harmony
patches, 45 settings). All four now run side by side with 0 errors on the Mods
screen. Answers below are from that work.

## 1. What did the ModdingDocs not include, or answer slowly?

- **What happens to saved references when a definition disappears.** Saves
  store a DefId as its uid, and `DefIdFormatter` turns a uid with no matching
  definition into an empty DefId. A character status saved that way then
  breaks every screen that looks it up: the Mage Sheet throws in
  `GearUtils.GetUnlockedGearSlots` every frame, draws half the sheet and stops
  taking input. That is what uninstalling any status-adding mod, or renaming a
  mod id, will do to a school. The docs and the save-mismatch dialog say
  nothing about it. Even registering the old key again from `OnLoad` did not
  make the saved status resolve; I had to re-link it in `OnWorldReady` using
  the `Config` it still carried.
- **Which load steps run before `OnLoad`.** `ResearchTechCatalog.Finalize` has
  already run, so a Harmony patch on it from `OnLoad` never fires. A short list
  of what is finished by each hook would save digging.
- **Mod room types are never recognised (bug).** `RoomTypeCatalog` builds
  `RoomTypesOrdered` in `PostDeserialize` while the base catalog loads, before
  mod definitions join it, and nothing rebuilds it. My Dark Temple showed every
  requirement ticked in the Room Goal panel but the room stayed a School Room
  until I re-ran `PostDeserialize` from `OnLoad`. No warning anywhere.
- **What `gameVersion` should be.** The examples say `"1.0.0"`; the build
  reports `v1.0.546`. Which does the loader compare?
- **Settings store types.** A `Dictionary<string, string>` property persisted
  and round-tripped fine; it would help to say which types are supported.
- What worked very well: the `OnInitialize` / `OnLoad` / `OnWorldReady` split,
  the simulation-process section (thread rules, cadence, yielding), and the
  "patch the caller, beware inlining" advice.

## 2. What did the Mod Tools viewer not include?

- A way to **export the Definitions tree to a file**. I work from the command
  line, so to confirm my overrides landed I wrote a throwaway mod that logged
  the definitions from `OnLoad`. A dump would serve scripts and diffs too.
- A view of **live characters' statuses that do not resolve** to a
  definition. It would have found the broken Mage Sheet above in seconds; I
  needed a diagnostic pass over every `CharacterStatusComponent`.

## 3. What did you want configurable, and did the field types cover it?

- Archmage Progression's 45 settings fit Bool, Int, Float and Text. The label
  width pushed me to short labels with the detail in tooltips, which worked.
- A comma-separated list of item keys had to be a Text field. A list or
  multi-select field over definition keys would be safer.
- **YAML-only mods cannot have settings.** Faction Balance had two numbers;
  offering them would need a C# assembly and bring the full-access warning to
  an otherwise data-only mod, so they became values in its YAML. A declarative
  setting in `mod.yaml` that feeds a patch `Value` would cover this.
- Settings only apply on the next launch. Most of mine are read when content
  loads, so that is fine, but being able to change some in a running game
  would be nice.

## 4. Where did YAML run out, and did C# work?

- Faction Balance stayed pure YAML, but only through whole-definition
  overrides: stat growth ranks and combat stats are dictionaries and the trait
  modifiers a list of same-typed entries, none of which a patch path can reach.
  A path segment for a dictionary key (`StatGrowthBases/Power`) and a list
  selector (`CharacterStatModifiers/[ModifiedStat=HP]`) would make it composable.
- Sacrificial Altar's research tech went from ~150 lines of C# under BepInEx to
  20 lines of YAML. Big win.
- Archmage Progression's behaviour needs C#, and the official route worked: the
  same source builds for BepInEx and for the beta.

### 4.1 Did the Harmony guide get me there?

Yes for the mechanics. Every target came from reading the game's code with a
decompiler and my own IL inspection tools, as the guide says to. Places I had
to work things out:

- The beta's own `ConfigBundle.Load` overload made an old BepInEx patch
  ambiguous; worth noting for anyone migrating.
- Runtime-built prefabs: a mod cannot ship one, and the altar's model is
  built at runtime. Both validators flag its `PrefabKey`, at definition load and
  after `OnLoad`. I add the path to `PrefabResourcePaths.Instance.Prefabs` in
  `OnInitialize`. A supported way to register a runtime prefab would be cleaner.
- A temporary catalog's constructor sets the static `Instance`. Under BepInEx
  this left the ingredient index resolving against a two-entry catalog and
  throwing at startup. Worth a warning in the docs for anyone who builds
  catalogs in code.

### 4.2 What was I reacting to?

Most of my patches are "when X happens":

- a character's trained skill level changes (`SkillsComponent.ModifySkillValue`);
- a ritual completes, and whether it may start (`RitualSiteComponent`);
- a battle starts, a round starts, a foe dies, damage is dealt;
- a status is about to be added (immunity);
- the clock reaches midnight or noon;
- a crafted recipe produces its output;
- a mental break ends in a knockout;
- quest travel starts; scouting completes; a dungeon run completes;
- a building's research lock and build-menu listing;
- stat tooltips are built.

Events for skill change, ritual complete, battle start and round start, status
add and remove, and time-of-day boundaries would replace about half my patches.

## 5. State with nothing in the world to hang it on?

Yes. The Archmage Council is a count over the whole school, so I put its badge
on the founder. A revival queue and once-per-load migrations live in static
fields, and per-mage cooldowns are hidden statuses. A small per-save key/value
store for a mod would have been cleaner than using statuses as storage.

## 6. When a mod broke the game, what happened?

- **Simulation crash** from a status carrying both `TeachBonusPct` and
  `SkillProgressModifiers`. `SkillsUtils.SkillChangeForLearn` adds the
  student's learning statuses and the teacher's teaching statuses to one
  dictionary, so one status on both is a duplicate key. The critical-error
  screen appeared and the game saved; `Player.log` had the stack trace and the
  key, and the namespaced key named my mod. Easy to diagnose; a `TryAdd` there
  would make the game tolerant of it.
- **Mage Sheet frozen** (see 1). `Player.log` had about 1,500 identical stack
  traces but nothing naming the status or the mod. A null check in
  `GetUnlockedGearSlots`, or dropping unresolved DefIds on load with a warning,
  would turn a frozen screen into a log line.
- **Interrupted startup**: the guard started the next session with mods off
  and said so. Exactly right.

### 6.1 Were Player.log and ModLog.txt enough?

For crashes, yes. For silent failures, such as the room type never matching or
a status that no longer resolves, there was nothing to read; I had to add my own
logging.

## 7. YAML configs with dependencies I did not know about?

- Mod room types need the ordered index rebuilt (see 1); nothing says so.
- A status must not carry both a teaching and a learning bonus (see 6).
- A `RenderConfig` needs a prefab that exists in the path list; the validator
  caught this, which was good.
- Research techs need their Codex tags, tier and layout to sit correctly in
  the tree; copying a base-game entry, as the docs advise, got this right first
  time.

## 8. Did warning and error levels match severity?

Mostly. Two exceptions:

- The runtime prefab was reported twice, as a warning at definition load and
  as an error after `OnLoad`. The same condition at two severities was
  confusing. For a mod that supplies the prefab itself, neither applies.
- Nothing at all was reported for the two most damaging cases, a mod room type
  that can never match and saved statuses that no longer resolve.
