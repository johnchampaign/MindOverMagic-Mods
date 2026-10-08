using System;
using HarmonyLib;
using Model;

namespace CharacterLevelRelics;

/// <summary>Host-independent identity and logging; each host fills in the logger.</summary>
internal static class Mod
{
    public const string PluginGuid = "ca.johnc.mindovermagic.characterlevelrelics";
    public const string PluginName = "Character Level Relics";
    public const string PluginVersion = "1.2.0";

    internal static Action<string> LogWarning { get; set; } = _ => { };
}

[HarmonyPatch(typeof(ArtifactUtils), nameof(ArtifactUtils.GetRelicLevelCap),
    new[] { typeof(Entity), typeof(int) })]
internal static class RelicLevelCapPatch
{
    private static bool _noLevelLogged;

    [HarmonyPrefix]
    private static bool UseCharacterLevel(Entity __0, ref int __result)
    {
        if (!CharacterLevelUtils.TryGetCharacterLevel(__0, out var characterLevel))
        {
            if (!_noLevelLogged)
            {
                _noLevelLogged = true;
                Mod.LogWarning("Relic source has no character level; using the vanilla relic-level calculation.");
            }

            return true;
        }

        __result = Math.Max(1, Math.Min(characterLevel + GetTrialBonus(__0), 24));
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
