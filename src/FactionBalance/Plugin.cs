using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Model;

namespace FactionBalance;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public sealed class Plugin : BaseUnityPlugin
{
    public const string PluginGuid = "ca.johnc.mindovermagic.factionbalance";
    public const string PluginName = "Faction Balance";
    public const string PluginVersion = "1.0.0";

    internal static ManualLogSource ModLog { get; private set; } = null!;
    internal static ConfigEntry<float> CultistBaseHp { get; private set; } = null!;
    internal static ConfigEntry<float> CultistDamageBonus { get; private set; } = null!;

    private void Awake()
    {
        ModLog = Logger;
        CultistBaseHp = Config.Bind(
            "Raven Cult",
            "BaseHP",
            125f,
            "Base HP supplied by the Raven Cult faction trait (vanilla: 150). Restart required.");
        CultistDamageBonus = Config.Bind(
            "Raven Cult",
            "DamageBonus",
            10f,
            "Damage bonus supplied by the Raven Cult faction trait (vanilla: 20). Restart required.");

        new Harmony(PluginGuid).PatchAll();
        Logger.LogInfo($"{PluginName} {PluginVersion} loaded.");
    }
}

[HarmonyPatch(typeof(ConfigBundle), nameof(ConfigBundle.Load))]
internal static class ConfigBundleLoadPatch
{
    [HarmonyPostfix]
    private static void ApplyGrowthBalance(ConfigBundle __instance)
    {
        if (!__instance.FactionCatalog.TryGetDefinitionFromStringKey("Cultist", out var cultist) ||
            !__instance.FactionCatalog.TryGetDefinitionFromStringKey("Glass", out var shattered))
        {
            Plugin.ModLog.LogError("Could not find the Raven Cult or Shattered faction definition.");
            return;
        }

        if (!__instance.StatGrowthCatalog.TryGetDefId("StatGrowth_A", out var rankA) ||
            !__instance.StatGrowthCatalog.TryGetDefId("StatGrowth_S", out var rankS))
        {
            Plugin.ModLog.LogError("Could not find the A or S stat-growth definition.");
            return;
        }

        cultist.StatGrowthBases[StatGrowthCategory.Power] = rankA;
        shattered.StatGrowthBases[StatGrowthCategory.Power] = rankS;

        Plugin.ModLog.LogInfo(
            "Applied faction growth balance: Raven Cult Power S -> A; Shattered Power A -> S.");

        if (!__instance.CharacterStatusCatalog.TryGetDefinitionFromStringKey(
                "CultistFaction", out var cultistTrait))
        {
            Plugin.ModLog.LogError("Could not find the Raven Cult faction trait.");
            return;
        }

        foreach (var modifier in cultistTrait.CharacterStatModifiers)
        {
            if (modifier.ModifiedStat == CharacterStatusConfig.ModifiedStat.HP)
            {
                modifier.ModifierAmount = Plugin.CultistBaseHp.Value;
            }
        }

        cultistTrait.CombatStats.Stats[CombatStat.DamageBonus] =
            Plugin.CultistDamageBonus.Value;

        Plugin.ModLog.LogInfo(
            $"Applied Raven Cult trait balance: base HP {Plugin.CultistBaseHp.Value}, " +
            $"damage bonus {Plugin.CultistDamageBonus.Value}.");
    }
}
