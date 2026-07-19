# Practical Mind Over Magic Modding Guide

This guide records the techniques, failure modes, and testing practices learned
while building the mods in this repository. It is intended for programmers
creating BepInEx plugins for the Windows Steam version of **Mind Over Magic**.

The examples were developed against:

- Mind Over Magic Steam build <code>20147148</code>
- Unity <code>2022.3.30</code>, as reported by the BepInEx log
- BepInEx <code>5.4.23.5</code> for Windows x64
- .NET Standard <code>2.1</code>
- Harmony through the BepInEx <code>0Harmony.dll</code>

Mind Over Magic has no compatibility promise for the internal APIs discussed
here. Treat class names, method signatures, definition keys, and load order as
version-specific implementation details. Recheck them after every game update.

These are unofficial modding notes. Follow the game's license and the laws that
apply where you live. Do not redistribute the game's assemblies or assets.

## 1. Start with the smallest appropriate kind of mod

There are three useful levels of intervention:

1. **Patch one calculation.** Use a narrow Harmony prefix or postfix when the
   game already has the behavior and you only need to change its result.
2. **Mutate loaded definitions.** Change catalog entries after YAML has loaded
   when rebalancing existing factions, traits, recipes, or research.
3. **Add content.** Define new archetypes, statuses, rooms, or wallpaper in YAML,
   inject those definitions at the correct lifecycle stage, and add only the
   runtime patches the new content requires.

Prefer the first level that can express the feature. A small calculation patch
has fewer save-game and update risks than a new persistent archetype.

Examples in this repository:

- [Character Level Relics](../src/CharacterLevelRelics/Plugin.cs) replaces one
  relic-cap calculation and falls back to the original method when its
  assumptions are not met.
- [Faction Balance](../src/FactionBalance/Plugin.cs) mutates existing catalog
  definitions after they load.
- [Sacrificial Altar](../src/SacrificialAltar/Plugin.cs) combines injected YAML,
  a procedural prefab, research and build-menu integration, UI patches, and
  ritual-completion bookkeeping.

## 2. Set up a reproducible project

Reference the locally installed game and BepInEx assemblies; do not copy them
into source control or a release archive.

A shared MSBuild properties file keeps paths in one place:

~~~xml
<Project>
  <PropertyGroup>
    <GameDir Condition="'$(GameDir)' == ''">C:\Program Files (x86)\Steam\steamapps\common\MindOverMagic</GameDir>
    <GameManagedDir>$(GameDir)\mindovermagic_Data\Managed</GameManagedDir>
    <BepInExCoreDir>$(GameDir)\BepInEx\core</BepInExCoreDir>
  </PropertyGroup>
</Project>
~~~

Each plugin can then target .NET Standard 2.1 and use non-copying references:

~~~xml
<TargetFramework>netstandard2.1</TargetFramework>

<Reference Include="BepInEx">
  <HintPath>$(BepInExCoreDir)\BepInEx.dll</HintPath>
  <Private>false</Private>
</Reference>
<Reference Include="0Harmony">
  <HintPath>$(BepInExCoreDir)\0Harmony.dll</HintPath>
  <Private>false</Private>
</Reference>
<Reference Include="Simulation">
  <HintPath>$(GameManagedDir)\Simulation.dll</HintPath>
  <Private>false</Private>
</Reference>
~~~

Add <code>View.dll</code>, <code>ViewState.dll</code>, and the necessary Unity
modules only when the plugin actually uses their types. Setting
<code>Private=false</code> prevents build output from collecting dependencies
that belong to the game or BepInEx.

Allow a non-default Steam library at build time:

~~~powershell
dotnet build src\MyMod\MyMod.csproj -c Release -p:GameDir="D:\SteamLibrary\steamapps\common\MindOverMagic"
~~~

For content files, preserve the directory structure in build output:

~~~xml
<Content Include="Content\**\*.yaml"
         CopyToOutputDirectory="PreserveNewest" />
~~~

Use a unique reverse-domain plugin ID and log the name and version during
<code>Awake</code>. That one line is the fastest way to distinguish “BepInEx
did not load” from “the mod loaded but its behavior is wrong.”

## 3. Separate source content from runtime content

A practical content layout is:

~~~text
MyMod/
├── MyMod.dll
└── Content/
    ├── CharacterStatus/
    ├── Entities/
    ├── RoomTypes/
    └── Wallpaper/
~~~

At runtime, derive the content root from <code>Paths.PluginPath</code>, not the
working directory:

~~~csharp
Path.Combine(Paths.PluginPath, "MyMod", "Content")
~~~

Use globally unique definition keys. Prefixing keys with the mod name is safest
for large projects. If an early prototype uses a short key, treat changing it
later as a save migration because saves may persist that key.

Localized text definitions should contain both a stable localization key and a
readable English fallback. The fallback keeps prototypes usable before a full
localization table exists.

## 4. Understand catalog timing before injecting definitions

Loading YAML is only part of registration. Catalogs often build indexes and
derived lookup tables after loading. Adding a definition after that step can
produce content that exists in one screen but is missing from another.

The reliable pattern used here is:

1. Patch the relevant catalog's <code>Load</code> method.
2. In a postfix, load only the mod's definitions into a temporary catalog.
3. Add those definitions to the live catalog.
4. Restore the appropriate static <code>Instance</code> reference if needed.
5. Let the game's normal post-load/finalization work run on the combined data.

Custom room types required an additional call to the room catalog's
<code>PostDeserialize</code> so its evaluator index included the new rules.
Without rebuilding that derived index, the Dark Temple could appear in the room
book and still never be recognized.

Three guards matter:

- A static <code>_injected</code> flag makes the operation idempotent.
- A thread-static <code>_loadingMod</code> flag prevents recursive patches when
  the temporary catalog calls the same <code>Load</code> method.
- A <code>try/finally</code> always clears the recursion flag.

Do not assume all catalogs have identical finalization. Inspect and test each
catalog separately.

### Mutating existing definitions

For balance changes, a postfix on <code>ConfigBundle.Load</code> is sufficient
when the definitions are already present and downstream indexes do not depend
on the values being changed.

Look definitions up by stable internal keys, never translated display names.
Check every lookup and log a useful error rather than dereferencing a missing
definition after a game update.

## 5. Keep Harmony patches narrow and defensive

Every patch should first decide whether the current call belongs to the mod.
For entity-specific behavior, compare the entity's internal key and immediately
return for all other entities.

Useful conventions:

- Return <code>true</code> from a prefix to run the original method.
- Return <code>false</code> only after assigning every required result.
- Use a postfix when the original result is mostly correct and needs a focused
  adjustment.
- Use Harmony <code>__state</code> to capture data before native cleanup and
  consume it after the original method completes.
- Null-check optional singletons and components.
- Log a warning and use vanilla behavior when a safe fallback exists.
- Do not log every frame. Track state changes and log only transitions.

The relic-cap mod demonstrates a good fallback: if the source entity has no
character level, it allows the vanilla relic calculation to run. That avoids
breaking relics created by an unanticipated non-character source.

Patching compiler-generated closure methods can solve UI filtering problems,
but it is fragile. Resolve the closure and method through Harmony
<code>AccessTools</code>, isolate the patch behind an entity-key check, and
expect to revisit it after updates.

## 6. Building a complete furniture archetype

A furniture item needs more than a name and render prefab. The working altar
definition includes:

- Codex tags for archetype, buildable, and furniture
- <code>CompositeConfig</code> for furniture classification
- <code>DescriptionConfig</code>
- <code>PlacementConfig</code>
- <code>CellUseConfig</code>
- <code>AssembleCraftedErrandConfig</code> with costs, work, and a flatpack key
- <code>ShopConfig</code> with category and subcategory
- disassembly and disintegration configs
- <code>RenderConfig</code>
- <code>PriorityConfig</code>
- ritual and interaction configs when applicable

Missing construction or cell-use behavior can create a blue placement ghost
that cannot be selected, prioritized, supplied, or built. A visible shop tile
therefore proves only that <code>ShopConfig</code> loaded; it does not prove the
entity is a functional construction project.

Copy the *shape* of a comparable native definition, then replace keys, costs,
sizes, and behavior intentionally. Avoid blindly copying unrelated configs.

### Build-menu registration

Custom archetypes may not automatically appear in
<code>BuildableUtils.GetBuildableArchetypes</code>. A focused postfix can add
the custom archetype if it is absent. Always check for an existing entry first
so repeated calls do not create duplicates or menu flicker.

The shop category and subcategory keys must both exist. A definition can be
buildable but invisible if its <code>ShopConfig</code> points to an invalid
subcategory.

## 7. Research integration has two separate responsibilities

“Unlocked by research” means both:

1. The research screen displays the item as a reward.
2. The build menu withholds the item until the research is complete.

Adding a custom item to a research definition's <code>RewardKeys</code> handles
the research relationship, but a custom build-registration patch may still put
the item directly into the shop. Gate the live shop data separately by checking
<code>Simulation.ResearchCompletion</code> and removing or restoring the
item's <code>ShopConfig</code>.

Perform reward-key injection before the research catalog finalizes its derived
reward data. Make it idempotent by checking whether the key is already present.

The research UI does not necessarily resolve a reward through the static
<code>ConfigData.Instance</code>. Its reward-cache path reads
<code>IConfigBundleProvider.Instance.ConfigBundle.Archetypes</code>. An item can
therefore be correctly gated and present in <code>RewardKeys</code>, yet remain
absent from the research description when it was injected into a different or
temporary catalog. Register the definition idempotently in the exact archetype
catalog owned by the live config bundle before research finalization, and
validate that the bundle can resolve the reward key.

Apply the same rule to guard flags. A process-wide <code>alreadyInjected</code>
boolean is unsafe when the game may construct more than one catalog or config
bundle. Make initialization idempotent per target instance (or derive it from
the target's contents), so loading a temporary bundle cannot cause the live
bundle to be skipped later.

Constructing any <code>DefinitionCatalog&lt;T&gt;</code> changes that generic
catalog's static <code>Instance</code> in its constructor. Temporary catalogs
used to load mod YAML can therefore redirect later <code>DefId.GetDefinition()</code>
calls even when a config bundle contains the correct data. Restore the intended
static instance after temporary loading, and remember that a screen may receive
one definition object while re-resolving the same ID through another catalog.

For stubborn UI integration, validate at the consumer boundary. Immediately
before the selected research widget builds its reward cache, compare and
idempotently synchronize the screen definition, the static-catalog definition,
and the live-bundle definition. Log reference identity, reward counts, and
whether the exact catalog used by the UI resolves the custom archetype. A
successful load-time message is not proof that a later UI consumer sees the
same object graph.

Also distinguish missing data from clipped presentation. In one concrete case,
the final research widget saw three reward keys, all three definition objects
were identical across catalogs, and the custom archetype resolved successfully;
the native Dark Arts panel still displayed only its two original cards. Its
fixed-width horizontal reward row clipped the appended third card. When a native
topic already fills its visible reward capacity, create a dedicated research
definition with its own node and reward row instead of continuing to patch the
native topic.

Custom research definitions should be injected before
<code>ResearchTechCatalog.Finalize</code>, use a unique ID, declare their tier,
prerequisite keys, research stations, cost, reward, and an unoccupied layout
location. Gate the build menu against the custom research ID so the visible
topic and actual unlock condition cannot drift apart.

Do not target an inherited method with an attribute that asks Harmony to find
that method as declared on the derived type. For example,
<code>[HarmonyPatch(typeof(ResearchTechCatalog), nameof(ResearchTechCatalog.Load))]</code>
fails when <code>Load</code> is inherited from
<code>DefinitionCatalog&lt;ResearchTechDefinition&gt;</code>. Harmony reports an
undefined target method, and <code>PatchAll</code> throws. Patch the verified
declaring method with an exact signature, supply an explicit
<code>TargetMethod</code>, or use a different verified lifecycle hook such as
<code>ConfigBundle.PostLoad</code> before finalization.

Treat <code>PatchAll</code> as a transaction even though Harmony does not. If
one patch class fails, earlier classes may remain active. Catch initialization
failure, call <code>UnpatchSelf</code>, log that the plugin was disabled, and
return before loading custom content. A partially patched content mod can leave
catalog statics and bundle data inconsistent and prevent the game from reaching
its main menu.

Apply the same transaction discipline inside bundle injection. Restore every
generic catalog <code>Instance</code> touched by temporary loading in a
<code>finally</code> block, not only on the successful path. A caught content
exception is not safe if it leaves <code>DefId.GetDefinition()</code> pointing
at a temporary, incomplete catalog; native post-load code may then fail far
away in ingredient or recipe indexing.

Do not assume every definition catalog will scan an arbitrary mod content root
the same way. Verify discovery in the runtime log. If a small, isolated custom
definition is not discovered and its field schema is known, constructing it
directly in the live catalog can be safer than creating another temporary
catalog. Initialize every list and nested reward collection that native
finalization may enumerate, add the definition before finalization, restore the
live static instance, and immediately verify the new ID resolves.

For programmatically constructed definitions, assign the definition's own key
field (for research, <code>ResearchTechDefinition.Id</code>) before calling
<code>DefinitionCatalog.Add</code>. Passing the intended ID only to a later
lookup is insufficient: <code>Add</code> calls <code>GetKey()</code> and hashes
that string immediately, so an unset field fails with an argument-null error.

A key-only <code>DefId&lt;T&gt;</code> is not automatically valid for every lookup.
<code>DefinitionCatalog.TryGetDefinition(DefId&lt;T&gt;)</code> reads the ID's
numeric UID directly; it does not hash the populated <code>Key</code> first. A
new <code>DefId</code> with only <code>Key</code> set therefore has UID zero and
can falsely report that an inserted definition is missing. For load-time
deduplication, compare the stored definitions' string keys, use a verified
string-key lookup API, or retain the fully assigned ID written by
<code>DefinitionCatalog.Add</code>.

<code>RitualSiteConfig.NotShownInResearchUI</code> controls presentation of the
derived ritual-site reward. It does not repair an archetype reward key that the
live bundle cannot resolve. Decide separately whether research should show the
building's archetype card, the ritual-site reward, or both.

Test research in all three states:

- a new school before the topic is complete;
- the moment the topic completes;
- an existing save in which the topic was already complete before installing
  the mod.

## 8. Procedural Unity prefabs: visual and interactive concerns

A procedural model can avoid shipping an asset bundle and can reuse a native
material as a visual starting point. The altar is assembled from Unity
primitives and one small custom mesh.

Intercept prefab lookup for the exact custom prefab key. The UI may request an
<code>Entities/</code>-prefixed form of the same key, so log failed lookups and
handle both forms when the game does so.

### One root collider, native interaction layer

Primitive creation adds colliders automatically. Remove child colliders and use
one deliberate root collider that matches the footprint. Multiple overlapping
primitive colliders can make selection unpredictable.

Do not guess the layer for the root collider. Read it from a comparable native
selectable furniture prefab. A model can render perfectly while remaining
unclickable because its collider is on the wrong layer.

### A runtime “prefab” may still be a live scene object

The procedural template is parented under <code>PrefabResources</code>, but it
is still a live object in the scene. Leaving it at the resource object's origin
caused an unclickable altar to appear above the school's entrance.

Move the template far outside the playable world, such as
<code>Y = -10000</code>, and let the game's instantiation path place only its
copies. Do not disable the template unless you have verified that cloned
instances are re-enabled.

### Build icon and world model are different tests

Verify all of the following independently:

- the shop tile has an image;
- the placement ghost appears;
- the ghost can be selected;
- a mage can construct it;
- the completed model can be clicked;
- interaction points are reachable;
- the model remains correctly placed after save/load.

## 9. Artifact and relic selectors may need explicit integration

Adding an artifact slot to ritual YAML does not guarantee that the native
picker understands the new ritual. The altar initially opened an empty relic
list even though the school owned relics.

The working integration does two things:

- changes the artifact picker use from <code>None</code> to an existing native
  relic-changing mode for this ritual site;
- supplies school <code>ArtifactComponent</code> candidates while respecting
  assignment and provisional-assignment state.

Do not list unavailable artifacts as selectable. Preserve the currently
selected artifact as a valid choice so reopening the picker does not invalidate
the ritual.

UI list bugs can look like inventory bugs. Confirm actual school inventory
through the simulation components before changing the content definition.

## 10. Distinguish ritual targets from ritual ingredients

The most destructive bug encountered here was a successful ritual that killed
the mage and then deleted the selected relic.

The cause was semantic: a basic ritual's generic completion path treats
delivered inputs as consumable ingredients. For this ritual, the relic is a
*target* to modify, not a payment.

The safe completion pattern is:

1. In a prefix, capture the ritual site, relic, sacrifice, and calculated
   upgrade in Harmony <code>__state</code>.
2. Before native completion cleanup, remove the relic entity from the matching
   ritual errand's <code>DeliveredInputs</code>.
3. Allow the game's normal ritual completion to run so participant statuses,
   death, corpse creation, timing, and other native behavior remain intact.
4. In a postfix, verify the relic still exists.
5. Call the game's native relic-cap upgrade routine.
6. Clear the ritual slot and artifact assignment.
7. Remove delivered/unavailable marker components.
8. Place the relic beside the ritual site through the native placement API.
9. Log both the requested and actual increase.

Never “fix” this by recreating a destroyed relic from its definition key. That
would risk losing its identity, XP, generated properties, or other saved state.
Preserve the original entity.

Whenever a ritual accepts an object, explicitly decide which ownership model
applies:

- consumed ingredient;
- temporarily reserved tool;
- modified target;
- transformed input that produces a different output.

Then test cancellation, insufficient participants, save/load mid-ritual, normal
completion, and capped-result behavior.

## 11. Reuse native simulation behavior when possible

The altar uses native ritual statuses to perform the sacrifice and produce the
corpse. Custom code is limited to classifying the participant, calculating the
bonus, preserving and upgrading the relic, and returning it to the world.

This is more reliable than manually duplicating death, inventory, notification,
and cleanup logic.

When classification types overlap, test the most specific type first. The altar
checks Staff, then Apprentice, then defaults to Student. Reversing that order
could silently award the wrong bonus if staff or apprentices satisfy a broader
character condition.

When a native method requires a type that cannot be cleanly referenced because
of assembly-version conflicts, a tightly scoped reflective call can be safer
than importing an incompatible assembly. Resolve the exact method shape, check
for failure, and log a clear fallback. Reflection is not a substitute for
understanding the API.

## 12. Room recognition and room appearance are separate systems

A room type definition supplies:

- a stable room ID and category;
- display text and effects;
- priority;
- positive criteria, such as required entities and luxury;
- negative criteria, such as forbidden beds or teaching stations;
- keyword-rule mappings expected by the room system.

Use the exact internal archetype and luxury keys. “Candelabra” in player-facing
text does not prove the internal key is plural or spelled the same way.

Group required criteria with <code>AllTrue</code>. Group exclusions with
<code>NoneTrue</code>. Give every config-based criterion a useful display name
so the room-goal panel reads like a native room rather than exposing class
names.

A default wallpaper definition is a separate catalog entry linked by room ID.
It can reuse a coherent native material set for sidewalls, backwall, floor, and
hallway blocks. Mark it as the room's default if the treatment should apply
automatically when the room qualifies.

Test both directions:

- adding the final requirement upgrades the room and changes its treatment;
- removing a requirement downgrades the room cleanly.

## 13. Status effects should use game time and player-readable text

The school-wide grief effect is defined as content rather than hard-coded
per-character timers. Its definition includes:

- a stable status key;
- Mood/Status Codex tags;
- display name, description, flavor text, and log text;
- duration in game hours;
- the mood/Conviction bias.

Using <code>AppliedStatusAllMages</code> in the ritual definition delegates
application to the native ritual system. This naturally includes the current
school population and keeps duration in simulation time.

Check the units of need modifiers carefully. In this build, a
<code>TargetBias</code> of <code>-0.10</code> represents the requested
-10 Conviction effect. Do not assume UI points and serialized decimals use the
same scale in every system.

## 14. Diagnose from evidence, not from the visible symptom alone

The BepInEx log is the first diagnostic surface:

~~~text
BepInEx\LogOutput.log
~~~

At startup, log:

- plugin name and version;
- successful catalog injections;
- successful research-reward registration;
- important fallback paths;
- failures with the definition or method key involved.

Good milestone logs made it possible to prove that the Dark Arts reward key had
been added even while the UI failed to display it. Inspecting the UI lookup path
then showed that the key was resolved through the live config bundle rather than
the static catalog. Log and validate both the mutation and the catalog lookup.

Useful read-only investigation techniques include:

- inspecting public and private types in the game's managed assemblies;
- listing method signatures and fields through reflection metadata;
- examining IL for load order and derived-cache construction;
- comparing a custom feature to one native feature at a time;
- checking the installed DLL hash against the build output;
- reviewing current and previous BepInEx logs.

Do not patch by guessed method name if you can verify the signature. Harmony can
silently become ineffective after an update when overloads change.

### Symptom-to-cause checklist

| Symptom | Likely area to inspect |
| --- | --- |
| No BepInEx log | BepInEx installation or launch path |
| Plugin absent from startup log | DLL location, target framework, dependency load |
| Build menu flickers | duplicate entries, repeated mutation, invalid config, exception during refresh |
| Shop tile missing | buildable list, shop category/subcategory, research gate |
| Shop tile has no image | prefab lookup path or UI form of the prefab key |
| Blue ghost cannot be clicked or built | root collider layer, cell-use/configuration, construction errand |
| Completed object cannot be clicked | collider, layer, selection footprint |
| Room is listed but never recognized | rule keys or stale room evaluator index |
| Room qualifies but appearance is unchanged | wallpaper catalog, room link, default flag, material keys |
| Artifact picker says “No Relics” | picker mode or candidate filtering, not necessarily inventory |
| Ritual deletes its target | native delivered-input consumption |
| Unclickable model appears near entrance | live procedural template left at resource origin |
| Research gates item but does not show reward | live bundle archetype lookup, reward finalization, or ritual-site presentation flag |

## 15. Use an incremental in-game test ladder

Complex content should be tested in this order:

1. BepInEx loads the plugin with no exception.
2. All custom catalogs report successful injection.
3. The build menu opens without flicker.
4. The item appears only in the intended research state.
5. The research topic displays the reward.
6. The shop tile has the correct icon and cost.
7. Placement is valid and selectable.
8. A mage supplies and constructs the item.
9. The completed object is selectable and its UI opens.
10. Every picker lists the expected candidates.
11. The ritual can begin and finish.
12. Inputs, targets, participants, output, status effects, and corpse state are
    all correct.
13. Room recognition and room treatment update in both directions.
14. Save, quit, reload, and recheck the entity, room, relic, and statuses.
15. Test on both a new school and an established save.

Close the game before replacing a plugin DLL or content file. Even if Windows
allows a file operation, the running process has already loaded the old
assembly and configuration, so the test would not represent the new build.

Keep a deliberately small test save near the relevant research and resource
state. Back it up before testing destructive gameplay features.

## 16. Package only what players need

A release ZIP should be directly extractable into the game directory:

~~~text
BepInEx/
└── plugins/
    └── MyMod/
        ├── MyMod.dll
        └── Content/
~~~

Include documentation and a license at the ZIP root if useful. Exclude:

- PDB and dependency files unless intentionally required;
- game and Unity assemblies;
- BepInEx assemblies;
- compiler output unrelated to the plugin;
- local configuration, saves, logs, and test tools;
- source art that the runtime does not use.

Build Release configuration, stage into a clean directory, compress that
directory, list the archive entries, and calculate a SHA-256 checksum. After
uploading a release, download the hosted asset and compare its checksum to the
local package.

Use a new version and release for a correction rather than silently replacing
an existing public asset. Keep the plugin version, README version table,
changelog, package name, tag, and release title synchronized.

The packaging script in this repository validates that its staging path is
inside <code>dist</code> before deleting an old staging tree. Apply the same
principle to every automated cleanup: resolve and validate the exact path before
recursive deletion.

## 17. Plan for save compatibility

Calculation patches and mutations of base definitions are usually easier to
remove than new persistent content, but they can still change ongoing
simulation balance.

A content mod can leave saves referring to:

- custom archetypes;
- custom room types;
- custom statuses;
- custom wallpaper;
- in-progress errands or rituals;
- assignments to custom entities.

Document safe removal. For a furniture mod, that generally means demolishing
all custom objects, finishing or canceling its errands, allowing temporary
statuses to expire, saving under a new name, and retaining the old save.

Never promise that removing a content mod is safe unless that exact migration
has been tested. Back up saves before installation, updates that change
definition structure, and uninstallation.

## 18. Design for game updates

Record the tested game build in the README and bug-report template. After a game
update:

1. Launch once with logging enabled.
2. Confirm every Harmony patch still resolves.
3. Confirm all internal definition keys still exist.
4. Recheck catalog load/finalization order.
5. Run the full in-game test ladder.
6. Rebuild against the updated local assemblies.
7. Publish a compatibility release only after save/load testing.

Prefer stable behavior-level patches over large copies of native logic. The
more native work the game still performs, the less code must be reconciled
after an update.

## 19. A practical pre-release checklist

- [ ] Plugin IDs and definition keys are unique.
- [ ] Every patch is restricted to the intended entity or feature.
- [ ] Missing lookups fail safely and log useful keys.
- [ ] Custom catalog injection is idempotent and recursion-safe.
- [ ] Derived catalog indexes are rebuilt at the correct time.
- [ ] Research reward display and actual unlock gating both work.
- [ ] Shop icon, placement, selection, construction, and interaction work.
- [ ] Procedural template objects are outside the playable world.
- [ ] Artifact targets survive ritual completion with identity and XP intact.
- [ ] Room qualification and default wallpaper update correctly.
- [ ] Temporary effects expire in simulation time.
- [ ] New and established saves both load after the change.
- [ ] Uninstallation risks and steps are documented.
- [ ] Release output contains no game, Unity, or BepInEx assemblies.
- [ ] ZIP entries and SHA-256 checksum are verified.
- [ ] The uploaded asset is downloaded and checksum-verified.
- [ ] README, changelog, plugin versions, tag, and release title agree.

## Further examples

The source in this repository is intentionally small enough to use as a set of
working examples:

- [Shared build paths](../Directory.Build.props)
- [Calculation patch with vanilla fallback](../src/CharacterLevelRelics/Plugin.cs)
- [Loaded-definition rebalance with configuration](../src/FactionBalance/Plugin.cs)
- [Content injection, prefab creation, research, ritual, and UI integration](../src/SacrificialAltar/Plugin.cs)
- [Furniture and ritual definition](../src/SacrificialAltar/Content/Entities/sacrificial_altar.yaml)
- [Room rules](../src/SacrificialAltar/Content/RoomTypes/dark_temple.yaml)
- [Default room treatment](../src/SacrificialAltar/Content/Wallpaper/dark_temple.yaml)
- [Timed school-wide status](../src/SacrificialAltar/Content/CharacterStatus/sacrificed_mage_grief.yaml)
- [Release packaging](../scripts/package.ps1)
