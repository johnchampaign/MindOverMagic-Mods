# Official-mod-support ports

These folders are mods for the game's own mod support, which is currently only
on the `mod_testing` Steam beta (game build v1.0.546). They do not use BepInEx.
Players on the public build should keep using the BepInEx modules in `src/`.

| Mod | Version | Kind | Replaces |
| --- | --- | --- | --- |
| `johnc.factionbalance` | 2.0.0 | YAML only, no code | Faction Balance 1.0.0 (BepInEx) |
| `johnc.archmageprogression` | 0.3.0 | C# and YAML | Archmage Progression 0.2.1 (BepInEx) |
| `johnc.characterlevelrelics` | 1.2.0 | C# | Character Level Relics 1.1.0 (BepInEx) |

## Installing

1. Copy the mod's folder into the `Mods` folder beside `mindovermagic.exe`.
   The Mods screen's **Open Mods Folder** button opens it.
2. Start the game, open **Mods** from the main menu, tick the mod and press
   **Restart**.

Do not run a port alongside its BepInEx version: both would apply the same
change. The BepInEx Faction Balance 1.0.0 also fails to load on the beta,
which added a second `ConfigBundle.Load` overload.

`johnc.factionbalance` is complete as it sits in this folder. The code mods
are built: `dotnet build src/ArchmageProgression.Official -c Release` (or
`src/CharacterLevelRelics.Official`) assembles the installable folder in
`dist/official/<modId>` from this folder's `mod.yaml`, the compiled DLL, and
any definitions shared with the BepInEx build.

## Faction Balance 2.0.0

The same balance as the BepInEx module's defaults:

| Change | Base game | Modded |
| --- | ---: | ---: |
| Raven Cult Power growth | S | A |
| Shattered Power growth | A | S |
| Raven Cult faction-trait base HP | 150 | 125 |
| Raven Cult faction-trait damage bonus | 20 | 10 |

It is two definition overrides and needs no code. Growth ranks, the trait's
stat modifiers and its combat stats are a dictionary, a list of same-typed
entries and a dictionary, which patches cannot reach into, so the overrides
restate each one whole and change only the values above. Everything else on
those definitions keeps its base-game value.

The BepInEx version's two config settings are gone: a settings screen needs a
code mod, which would bring the game's full-access warning to a mod that
otherwise has none. To change the numbers, edit
`Defs/CharacterStatus/raven_cult.yaml` and restart.

Like any mod on the beta, a school played with it enabled stops earning Steam
achievements.

## Archmage Progression 0.3.0

The same features and defaults as the BepInEx build, from the same source:
`src/ArchmageProgression.Official` compiles `src/ArchmageAscension`'s
`Plugin.cs` and `Settings.cs` with `OFFICIAL_MOD` defined, plus its own
`OfficialEntry.cs`. What differs:

| | BepInEx build | Official build |
| --- | --- | --- |
| Statuses and buildings | Injected into the game's catalogs from `Content/` | Loaded by the game from `Defs/` |
| Definition keys | `ArchmageProgression_...` | `johnc.archmageprogression.ArchmageProgression_...` (the game namespaces them) |
| Settings | `BepInEx\config\ca.johnc.mindovermagic.archmageprogression.cfg` | **Settings** on the Mods screen, stored in `ModSettings\johnc.archmageprogression.yaml` |
| Periodic sweep | Postfix on the game's skill-status system | A registered simulation process, every game-second |
| Harmony | BepInEx's | The game's, through the mod's own instance |

Settings keep the BepInEx names internally (`Section/Key` in the settings
file) but show shorter labels on screen, with the full explanation on hover.
They take effect on the next launch.

**Saves from the BepInEx build.** Those saves hold rank and Council statuses
under the old, un-namespaced keys. The official build registers those keys as
aliases so the save resolves them, then on the first sweep after loading it
removes every old status; the sweep grants the new ones straight after, so the
log book may show each rank being lost and earned again once. Nexus Gateways
and Waters of Return fountains are not aliased: demolish them before moving a
save from the BepInEx build to this one.

## Character Level Relics 1.2.0

The same relic level cap as the BepInEx build, from the same patch:
`src/CharacterLevelRelics.Official` compiles `src/CharacterLevelRelics`'s
`RelicLevelCap.cs` with its own `OfficialEntry.cs`.

```text
level cap = min(24, mage level + completed trial bonuses)
```

Each earned tier 0 or 1 trial adds 1, tier 2 adds 2 and tier 3 adds 3. Wand
type, wand tier and apprenticeship no longer count. It has no settings and no
definitions; it is one Harmony prefix on `ArtifactUtils.GetRelicLevelCap`.
