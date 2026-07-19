using System;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Model;

namespace CharacterLevelRelics;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public sealed class Plugin : BaseUnityPlugin
{
    public const string PluginGuid = "ca.johnc.mindovermagic.characterlevelrelics";
    public const string PluginName = "Character Level Relics";
    public const string PluginVersion = "1.1.0";

    internal static ManualLogSource ModLog { get; private set; } = null!;

    private void Awake()
    {
        ModLog = Logger;
        new Harmony(PluginGuid).PatchAll();
        Logger.LogInfo($"{PluginName} {PluginVersion} loaded.");
    }
}

[HarmonyPatch(typeof(ArtifactUtils), nameof(ArtifactUtils.GetRelicLevelCap),
    new[] { typeof(Entity), typeof(int) })]
internal static class RelicLevelCapPatch
{
    [HarmonyPrefix]
    private static bool UseCharacterLevel(Entity __0, ref int __result)
    {
        if (!CharacterLevelUtils.TryGetCharacterLevel(__0, out var characterLevel))
        {
            Plugin.ModLog.LogWarning(
                "Relic source has no character level; using the vanilla relic-level calculation.");
            return true;
        }

        var trialBonus = GetTrialBonus(__0);
        __result = Math.Max(1, Math.Min(characterLevel + trialBonus, 24));

        Plugin.ModLog.LogDebug(
            $"Relic level cap set to {__result} from character level " +
            $"{characterLevel} and trial bonus {trialBonus}.");
        return false;
    }

    private static int GetTrialBonus(Entity entity)
    {
        if (!entity.TryGetComponent<BadgeOwnerComponent>(out var badgeOwner))
        {
            return 0;
        }

        var bonus = 0;
        foreach (var badge in badgeOwner.GoalProgress)
        {
            if (!badge.Value.Earned)
            {
                continue;
            }

            switch (badge.Key.GetDefinition().BadgeTier)
            {
                case 0:
                case 1:
                    bonus += 1;
                    break;
                case 2:
                    bonus += 2;
                    break;
                case 3:
                    bonus += 3;
                    break;
            }
        }

        return bonus;
    }
}
