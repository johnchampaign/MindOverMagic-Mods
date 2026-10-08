using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Model;
using Model.Modding;

namespace SacrificialAltar;

/// <summary>Host-independent identity and logging for the official-mod build.</summary>
internal static class Plugin
{
    public const string PluginName = "Sacrificial Altar";
    public const string PluginVersion = "1.4.0";

    internal static ModLogger ModLog { get; set; } = new(_ => { }, _ => { }, _ => { });
}

internal sealed class ModLogger
{
    private readonly Action<string> _info;
    private readonly Action<string> _warning;
    private readonly Action<string> _error;

    internal ModLogger(Action<string> info, Action<string> warning, Action<string> error)
    {
        _info = info;
        _warning = warning;
        _error = error;
    }

    internal void LogInfo(string message) => _info(message);
    internal void LogWarning(string message) => _warning(message);
    internal void LogError(string message) => _error(message);
}

/// <summary>
/// The host for the game's own mod support. The altar, its research, the Dark Temple and
/// the grief status arrive through the mod's Defs folder; the altar's model and the
/// sacrifice ritual are the same patches the BepInEx build runs.
/// </summary>
public sealed class OfficialEntry : IMod
{
    private const string LegacyRitesKey = "SacrificialRites";
    private const string LegacyGriefKey = "SacrificedMageGrief";
    private DefId<ResearchTechDefinition>? _ritesId;

    public void OnInitialize(IModContext context)
    {
        Plugin.ModLog = new ModLogger(context.Logger.Log, context.Logger.LogWarning, context.Logger.LogError);

        // The altar's model is built at runtime by AltarPrefabPatch, so it is missing from
        // the game's list of prefab paths, and the game's reference checks would report the
        // altar as having no visual. List it before the definitions that name it are read.
        if (PrefabResourcePaths.Instance?.Prefabs is { } prefabs)
        {
            if (!prefabs.Contains(AltarPrefabPatch.ShopPrefabKey))
            {
                prefabs.Add(AltarPrefabPatch.ShopPrefabKey);
            }
        }
        else
        {
            Plugin.ModLog.LogWarning("The game's prefab list was not ready; the altar may be reported as having no visual.");
        }
    }

    public void OnLoad(ConfigBundle configBundle, IModContext context)
    {
        if (!configBundle.Archetypes.TryGetArchetypeFromStringKey(Keys.Altar, out _) ||
            !configBundle.ResearchTechCatalog.TryGetDefId(Keys.Rites, out var ritesId))
        {
            Plugin.ModLog.LogError(
                $"The altar ({Keys.Altar}) or its research ({Keys.Rites}) did not load from Defs; " +
                "the mod is disabled.");
            return;
        }

        // The research catalog is finalized before this hook, so the patch that checks the
        // graph in the BepInEx build never fires here; check the finished graph directly.
        SacrificialRitesGraphValidationPatch.Validate(configBundle.ResearchTechCatalog);
        _ritesId = ritesId;
        IndexDarkTemple(configBundle.RoomTypeCatalog);
        AliasLegacyGrief(configBundle.CharacterStatusCatalog);

        var harmony = (Harmony)context.HarmonyInstance;
        try
        {
            harmony.PatchAll(typeof(OfficialEntry).Assembly);
        }
        catch (Exception exception)
        {
            // PatchAll can leave classes patched before a later invalid target throws.
            // Roll everything back so the base game can still start.
            harmony.UnpatchAll(harmony.Id);
            Plugin.ModLog.LogError($"{Plugin.PluginName} could not initialize and was safely disabled: {exception}");
            return;
        }

        Plugin.ModLog.LogInfo($"{Plugin.PluginName} {Plugin.PluginVersion} loaded.");
    }

    /// <summary>
    /// The game builds the ordered list of room types it matches rooms against when its own
    /// catalog loads, before mod definitions join the catalog, and does not rebuild it. A
    /// mod's room type is then never recognised even when the Room Goal panel shows every
    /// requirement met. Rebuild the list once the Dark Temple is in the catalog.
    /// </summary>
    private static void IndexDarkTemple(RoomTypeCatalog rooms)
    {
        if (rooms.RoomTypesOrdered?.Exists(room => room.Id.Key == Keys.DarkTemple) == true)
        {
            return;
        }

        var rebuild = AccessTools.Method(typeof(RoomTypeCatalog), "PostDeserialize");
        if (rebuild is null)
        {
            Plugin.ModLog.LogError("Could not rebuild the room-type index; rooms will not become Dark Temples.");
            return;
        }

        rebuild.Invoke(rooms, null);
        var indexed = rooms.RoomTypesOrdered?.Exists(room => room.Id.Key == Keys.DarkTemple) == true;
        if (indexed)
        {
            Plugin.ModLog.LogInfo("Rebuilt the room-type index so rooms can be recognised as Dark Temples.");
        }
        else
        {
            Plugin.ModLog.LogError("The Dark Temple is still missing from the room-type index; rooms will not become Dark Temples.");
        }
    }

    /// <summary>
    /// Saves made with the BepInEx build can carry the grief status under its un-namespaced
    /// key for two game days after a sacrifice. Register that key too, so those mages resolve
    /// it; it expires on its own.
    /// </summary>
    private static void AliasLegacyGrief(CharacterStatusConfigCatalog catalog)
    {
        const string legacyKey = LegacyGriefKey;
        if (catalog.TryGetDefinitionFromStringKey(legacyKey, out _) ||
            !catalog.TryGetDefinitionFromStringKey(Keys.Grief, out var grief))
        {
            return;
        }

        var clone = typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic);
        if (clone?.Invoke(grief, null) is not CharacterStatusConfig alias)
        {
            Plugin.ModLog.LogWarning("Could not alias the grief status saved by the BepInEx build.");
            return;
        }

        alias.Id = new DefId<CharacterStatusConfig>
        {
            Key = legacyKey,
            Uid = DefinitionCatalog<CharacterStatusConfig>.GenerateUid(legacyKey)
        };
        catalog.Add(alias);
    }

    /// <summary>
    /// A save loads each status by its id, and a grief status saved by the BepInEx build comes
    /// back with an empty id even with the alias registered, though it still carries its
    /// definition. The game then fails wherever it looks the status up (the Mage Sheet's gear
    /// slots stop the sheet drawing), so move each such status onto this build's grief status.
    /// </summary>
    private static void RelinkLegacyGrief(Simulation simulation)
    {
        if (DefinitionCatalog<CharacterStatusConfig>.Instance is not { } catalog ||
            !catalog.TryGetDefinitionFromStringKey(Keys.Grief, out var grief))
        {
            return;
        }

        var relinked = 0;
        var broken = new List<DefId<CharacterStatusConfig>>();
        try
        {
            foreach (var statuses in simulation.ComponentManager.AllEnumerator<CharacterStatusComponent>())
            {
                broken.Clear();
                foreach (var pair in statuses.Statuses)
                {
                    if (pair.Key.GetDefinition() is null &&
                        pair.Value?.Config?.Id.Key is LegacyGriefKey or Keys.Grief)
                    {
                        broken.Add(pair.Key);
                    }
                }

                foreach (var id in broken)
                {
                    var status = statuses.Statuses[id];
                    statuses.Statuses.Remove(id);
                    if (!statuses.Statuses.ContainsKey(grief.Id))
                    {
                        status.StatusId = grief.Id;
                        status.Config = grief;
                        statuses.Statuses[grief.Id] = status;
                    }

                    relinked++;
                }
            }
        }
        catch (Exception exception)
        {
            Plugin.ModLog.LogError($"Failed to repair grief statuses saved by the BepInEx build: {exception}");
            return;
        }

        if (relinked > 0)
        {
            Plugin.ModLog.LogInfo($"Repaired {relinked} grief statuses saved by the BepInEx build.");
        }
    }

    public void OnSimulationStart(Simulation simulation)
    {
    }

    public void OnWorldReady(Simulation simulation, SimulationStartReason reason, IModContext context)
    {
        RelinkLegacyGrief(simulation);

        // Saves made with the BepInEx build record the research under its un-namespaced key.
        // Carry it over so the school keeps the altar without researching it again.
        if (reason != SimulationStartReason.LoadedSave || _ritesId is not { } ritesId ||
            simulation.ResearchCompletion is not { } completed)
        {
            return;
        }

        var legacy = new DefId<ResearchTechDefinition>
        {
            Key = LegacyRitesKey,
            Uid = DefinitionCatalog<ResearchTechDefinition>.GenerateUid(LegacyRitesKey)
        };
        if (completed.Remove(legacy))
        {
            completed.Add(ritesId);
            Plugin.ModLog.LogInfo("Carried over Sacrificial Rites, completed in this save under the BepInEx build.");
        }
    }

    public void OnSimulationEnd()
    {
    }

    public void OnUnload()
    {
    }
}
