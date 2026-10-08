# Official-mod-support ports

These folders are mods for the game's own mod support, which is currently only
on the `mod_testing` Steam beta (game build v1.0.546). They do not use BepInEx.
Players on the public build should keep using the BepInEx modules in `src/`.

| Mod | Version | Kind | Replaces |
| --- | --- | --- | --- |
| `johnc.factionbalance` | 2.0.0 | YAML only, no code | Faction Balance 1.0.0 (BepInEx) |

## Installing

1. Copy the mod's folder into the `Mods` folder beside `mindovermagic.exe`.
   The Mods screen's **Open Mods Folder** button opens it.
2. Start the game, open **Mods** from the main menu, tick the mod and press
   **Restart**.

Do not run a port alongside its BepInEx version: both would apply the same
change.

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
