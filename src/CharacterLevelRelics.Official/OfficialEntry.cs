using System;
using HarmonyLib;
using Model;
using Model.Modding;

namespace CharacterLevelRelics;

/// <summary>The host for the game's own mod support; the patch is shared with the BepInEx build.</summary>
public sealed class OfficialEntry : IMod
{
    public void OnInitialize(IModContext context)
    {
    }

    public void OnLoad(ConfigBundle configBundle, IModContext context)
    {
        Mod.LogWarning = context.Logger.LogWarning;
        var harmony = (Harmony)context.HarmonyInstance;
        try
        {
            harmony.PatchAll(typeof(OfficialEntry).Assembly);
        }
        catch (Exception exception)
        {
            harmony.UnpatchAll(harmony.Id);
            context.Logger.LogError($"{Mod.PluginName} was safely disabled during initialization: {exception}");
            return;
        }

        context.Logger.Log($"{Mod.PluginName} {Mod.PluginVersion} loaded.");
    }

    public void OnSimulationStart(Simulation simulation)
    {
    }

    public void OnWorldReady(Simulation simulation, SimulationStartReason reason, IModContext context)
    {
    }

    public void OnSimulationEnd()
    {
    }

    public void OnUnload()
    {
    }
}
