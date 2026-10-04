# Mind Over Magic Mods

Four independent BepInEx 5 plugins for **Mind Over Magic**: character-level
relic caps, a focused faction rebalance, a fully playable Sacrificial Altar,
and an independently authored late-game magic progression module.

![Sacrificial Altar](src/SacrificialAltar/Assets/sacrificial-altar.png)

These are unofficial, fan-made mods. They are not affiliated with or supported
by Sparkypants Studios or Klei Entertainment.

## Modules

| Module | Version | What it changes |
| --- | ---: | --- |
| Character Level Relics | 1.1.0 | Bases manifested relic level caps on the mage's level and completed trials instead of wand tier. |
| Faction Balance | 1.0.0 | Reduces the Raven Cult's universal advantages and gives the Shattered the strongest Power growth. |
| Sacrificial Altar | 1.3.6 | Adds a buildable altar, dedicated Sacrificial Rites research, sacrificial relic-upgrade ritual, Dark Temple room, custom model, and room treatment. |
| Archmage Progression | 0.1.0 | Gives each magic school a configurable, passive per-skill benefit without editing base-game files. |

The modules do not depend on one another. Install any combination of them.

## Modding documentation

The [Practical Mind Over Magic Modding Guide](docs/MODDING-GUIDE.md) collects
the implementation patterns, debugging lessons, failure modes, testing
checklists, packaging practices, and save-safety guidance learned while
developing these mods.

## Requirements

- Mind Over Magic for Windows on Steam
- [BepInEx 5.4.23.5](https://github.com/BepInEx/BepInEx/releases)
  for Windows x64
- The release was built and tested against Mind Over Magic Steam build
  `20147148`

Game updates can change internal methods or data definitions and may require a
new version of these plugins.

## Installation

1. Install BepInEx 5 into the Mind Over Magic directory. This is the directory
   containing `mindovermagic.exe`.
2. Start the game once and close it, allowing BepInEx to create its folders.
3. Download either the **All Modules** ZIP or just the individual module ZIPs
   you want from the latest GitHub release. Each ZIP is independently
   extractable.
4. Extract every chosen ZIP into the Mind Over Magic directory. Merge the included
   `BepInEx` folder when prompted.
5. Start the game normally through Steam.

The release asset names are:

| Asset | Contents |
| --- | --- |
| `MindOverMagic-Mods-v<version>.zip` | All modules (convenience option) |
| `MindOverMagic-CharacterLevelRelics-v<version>.zip` | Character Level Relics only |
| `MindOverMagic-FactionBalance-v<version>.zip` | Faction Balance only |
| `MindOverMagic-SacrificialAltar-v<version>.zip` | Sacrificial Altar only |
| `MindOverMagic-ArchmageProgression-v<version>.zip` | Archmage Progression only |

The resulting layout is:

```text
MindOverMagic/
└── BepInEx/
    └── plugins/
        ├── CharacterLevelRelics/
        │   └── CharacterLevelRelics.dll
        ├── FactionBalance/
        │   └── FactionBalance.dll
        └── SacrificialAltar/
            ├── SacrificialAltar.dll
            └── Content/
                ├── CharacterStatus/
                ├── Entities/
                ├── RoomTypes/
                └── Wallpaper/
        └── ArchmageProgression/
            └── ArchmageProgression.dll
```

On a successful launch, `BepInEx\LogOutput.log` contains a `loaded` line for
each installed module.

## Character Level Relics

Relics manifested from mages use this formula:

```text
level cap = min(24, mage level + completed trial bonuses)
```

Every earned trial contributes independently:

| Modifier | Base game | With this module |
| --- | ---: | ---: |
| Wand Tier I base | 5 | 0 |
| Wand Tier II | +4 | 0 |
| Wand Tier III | +4 | 0 |
| Each tier 0 or tier 1 trial | +1 | +1 |
| Each tier 2 trial | +2 | +2 |
| Each tier 3 trial | +3 | +3 |
| Completed apprenticeship | +3 | 0 |
| Mage's current level | 0 | +1 per level |
| Maximum manifested cap | 24 | 24 |

Additional behavior:

- A mage can contribute up to 18 levels from character level.
- Wand type and wand tier no longer affect the result.
- The apprenticeship bonus is removed.
- Relic sources without a character level fall back to the original game
  calculation.
- Existing relics and their accumulated XP are not retroactively changed.

## Faction Balance

This module keeps Raven Cultists strong and flexible while making a full school
of Raven Cultists less automatically optimal.

| Change | Base game | Modded default |
| --- | ---: | ---: |
| Raven Cult Power growth | S | A |
| Shattered Power growth | A | S |
| Raven Cult faction-trait base HP | 150 | 125 |
| Raven Cult faction-trait damage bonus | 20 | 10 |

Other faction values are unchanged. Raven Cult HP and damage values can be
edited after the first launch in:

```text
BepInEx\config\ca.johnc.mindovermagic.factionbalance.cfg
```

Changes to that configuration require a game restart.

## Sacrificial Altar

The Sacrificial Altar is unlocked by the dedicated **Sacrificial Rites** Tier III
research topic. Sacrificial Rites requires **Dark Arts**, costs 3,000 research,
and makes the altar available under **Furniture > Rituals & Relics** when
completed.

### Construction

- 20 Stone
- 5 Viscera
- 5 Small Carcasses
- 48 construction casts

The altar uses an original procedural Unity model assembled by the plugin at
runtime. No game prefab or external asset bundle is redistributed.

### Sacrifice for Relic ritual

The ritual takes three in-game hours. Select one relic and one living mage:

| Sacrifice | Relic level-cap increase |
| --- | ---: |
| Student | +1 |
| Apprentice | +2 |
| Staff | +3 |
| Ritual performed in a Dark Temple | Additional +1 |

The relic is a ritual target, not an ingredient. After completion it retains
its identity and XP, receives the available cap increase through the game's
native relic-upgrade routine, and is released beside the altar.

The sacrificed mage dies and leaves a corpse that must be buried. Every mage
in the school receives **Sacrificed Mage**, a -10 Conviction penalty lasting
48 in-game hours.

### Dark Temple

A room qualifies as a Dark Temple when it contains:

- At least 1 Sacrificial Altar
- At least 2 Candelabras
- At least 20 Creepy luxury
- No beds
- No dining tables
- No teaching stations

Sacrifices performed there gain the additional +1 relic-cap bonus. Qualifying
rooms automatically receive the game's dark occult classroom-style walls,
backwall, floor, and hallway treatment.

## Archmage Progression (early access)

Archmage Progression is an independent, source-available interpretation of
late-game magic-school advancement. It does not reuse another mod's files,
assembly, assets, or code, and it never writes to Mind Over Magic's YAML files.

Version 0.1.0 implements the reliable passive foundation. Every point in these
schools provides a configurable benefit:

| School | Per-skill benefit | Default |
| --- | --- | ---: |
| Pyromancy | Maximum Mana | +5 |
| Geomancy | Maximum HP | +5 |
| Divination | Combat Damage Bonus | +2 |
| Manipulation | Combat Dodge Chance | +2 |
| Hydrokinesis | Combat regeneration | +1 |
| Necromancy | Slower ordinary need decay | 2% |

The current version intentionally does not claim to implement Viturgy's
conviction feature, custom Adept/Archmage powers, or school-wide council
effects. Those require separate validated game hooks and will be released only
when each can be tested without save-file edits or base-file replacement.

After its first launch, adjust the values in:

```text
BepInEx/config/ca.johnc.mindovermagic.archmageprogression.cfg
```

Restart the game after changing them.

## Updating

Close the game, extract the new release over the old files, and allow existing
files to be replaced. Do not leave multiple copies of the same plugin DLL in
different BepInEx subdirectories.

## Uninstalling and saves

`CharacterLevelRelics` and `FactionBalance` can be removed by deleting their
plugin folders while the game is closed.

`SacrificialAltar` adds custom definitions that can be referenced by saved
schools. Back up your saves before installing it. Before removing it, demolish
all Sacrificial Altars, allow any Sacrificed Mage status effects to expire, save
under a new name, and keep the old save as a backup. Removing a content mod from
a save that still references its custom entities can prevent the save from
loading correctly.

## Troubleshooting

- Confirm that `BepInEx\LogOutput.log` exists. If not, BepInEx itself is not
  installed or loading.
- Confirm that the plugin DLLs are under `BepInEx\plugins`, not beside the game
  executable and not inside an extra nested ZIP folder.
- Search `LogOutput.log` for `Character Level Relics`, `Faction Balance`, or
  `Sacrificial Altar`.
- The altar will not appear in the build menu until Sacrificial Rites has been
  completed in that school.
- Back up affected saves and report the game build, mod versions, and relevant
  BepInEx log lines with any bug report.

## Building from source

Install BepInEx into Mind Over Magic, then run:

```powershell
dotnet build src\CharacterLevelRelics\CharacterLevelRelics.csproj -c Release
dotnet build src\FactionBalance\FactionBalance.csproj -c Release
dotnet build src\SacrificialAltar\SacrificialAltar.csproj -c Release
dotnet build src\ArchmageAscension\ArchmageAscension.csproj -c Release
```

The projects assume Steam's default install location. Override it when needed:

```powershell
dotnet build src\SacrificialAltar\SacrificialAltar.csproj -c Release `
  -p:GameDir="D:\SteamLibrary\steamapps\common\MindOverMagic"
```

Run `scripts\package.ps1` to build all modules and create the distributable ZIP
under `dist`.

## License and permissions

The original source code and original altar artwork in this repository are
released under the [MIT License](LICENSE). Mind Over Magic, its code, assets,
names, and trademarks belong to their respective owners and are not covered by
this repository's license.

Do not redistribute Mind Over Magic assemblies with these mods. This project
references locally installed game assemblies only at build time.
