using System;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;

namespace ArchmageProgression;

/// <summary>
/// An independently authored, source-available late-game progression module.
/// It deliberately implements only mechanics whose game hooks are understood
/// and tested; unverified features are never simulated by editing save data or
/// base-game YAML files. This is the BepInEx host; the official-mod host is OfficialEntry.
/// </summary>
[BepInPlugin(Plugin.PluginGuid, Plugin.PluginName, Plugin.PluginVersion)]
public sealed class BepInExEntry : BaseUnityPlugin
{
    private void Awake()
    {
        Plugin.ModLog = new ModLogger(Logger.LogInfo, Logger.LogWarning, Logger.LogError);
        foreach (var setting in Plugin.All)
        {
            Bind(setting);
        }

        var harmony = new Harmony(Plugin.PluginGuid);
        try
        {
            harmony.PatchAll();
        }
        catch (Exception exception)
        {
            // PatchAll may have applied earlier patches before a later invalid target
            // fails. Undo them so a game update cannot leave a partial mod active.
            harmony.UnpatchSelf();
            Logger.LogError($"{Plugin.PluginName} was safely disabled during initialization: {exception}");
            enabled = false;
            return;
        }

        Logger.LogInfo($"{Plugin.PluginName} {Plugin.PluginVersion} loaded: passive magic-school scaling is active.");
    }

    private void Bind(Setting setting)
    {
        switch (setting)
        {
            case Setting<float> number:
                number.Value = Config.Bind(number.Section, number.Key, number.DefaultValue, number.Description).Value;
                break;
            case Setting<int> whole:
                whole.Value = Config.Bind(whole.Section, whole.Key, whole.DefaultValue, whole.Description).Value;
                break;
            case Setting<bool> flag:
                flag.Value = Config.Bind(flag.Section, flag.Key, flag.DefaultValue, flag.Description).Value;
                break;
            case Setting<string> text:
                text.Value = Config.Bind(text.Section, text.Key, text.DefaultValue, text.Description).Value;
                break;
        }
    }
}
