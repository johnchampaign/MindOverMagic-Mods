using System;
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

    public void OnSimulationStart(Simulation simulation)
    {
    }

    public void OnWorldReady(Simulation simulation, SimulationStartReason reason, IModContext context)
    {
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
