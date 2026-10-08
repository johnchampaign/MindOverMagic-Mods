using BepInEx;
using HarmonyLib;

namespace CharacterLevelRelics;

/// <summary>The BepInEx host; the official-mod host is OfficialEntry.</summary>
[BepInPlugin(Mod.PluginGuid, Mod.PluginName, Mod.PluginVersion)]
public sealed class Plugin : BaseUnityPlugin
{
    private void Awake()
    {
        Mod.LogWarning = Logger.LogWarning;
        new Harmony(Mod.PluginGuid).PatchAll();
        Logger.LogInfo($"{Mod.PluginName} {Mod.PluginVersion} loaded.");
    }
}
