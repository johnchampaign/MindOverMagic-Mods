using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Model;

namespace ArchmageProgression;

/// <summary>
/// An independently authored, source-available late-game progression module.
/// It deliberately implements only mechanics whose game hooks are understood
/// and tested; unverified features are never simulated by editing save data or
/// base-game YAML files.
/// </summary>
[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public sealed class Plugin : BaseUnityPlugin
{
    public const string PluginGuid = "ca.johnc.mindovermagic.archmageprogression";
    public const string PluginName = "Archmage Progression";
    public const string PluginVersion = "0.1.0";

    internal static ManualLogSource ModLog { get; private set; } = null!;
    internal static ConfigEntry<float> ManaPerPyromancy { get; private set; } = null!;
    internal static ConfigEntry<float> HitPointsPerGeomancy { get; private set; } = null!;
    internal static ConfigEntry<float> DamagePerDivination { get; private set; } = null!;
    internal static ConfigEntry<float> DodgePerManipulation { get; private set; } = null!;
    internal static ConfigEntry<float> RegenPerHydrokinesis { get; private set; } = null!;
    internal static ConfigEntry<float> NeedDecayReductionPerNecromancy { get; private set; } = null!;

    private void Awake()
    {
        ModLog = Logger;
        ManaPerPyromancy = Config.Bind(
            "Per-skill bonuses", "Mana per Pyromancy", 5f,
            "Maximum Mana added for each Pyromancy skill level. Restart required.");
        HitPointsPerGeomancy = Config.Bind(
            "Per-skill bonuses", "HP per Geomancy", 5f,
            "Maximum HP added for each Geomancy skill level. Restart required.");
        DamagePerDivination = Config.Bind(
            "Per-skill bonuses", "Damage bonus per Divination", 2f,
            "Combat Damage Bonus added for each Divination skill level. Restart required.");
        DodgePerManipulation = Config.Bind(
            "Per-skill bonuses", "Dodge per Manipulation", 2f,
            "Combat Dodge Chance added for each Manipulation skill level. Restart required.");
        RegenPerHydrokinesis = Config.Bind(
            "Per-skill bonuses", "Combat regeneration per Hydrokinesis", 1f,
            "Combat regeneration added for each Hydrokinesis skill level. Restart required.");
        NeedDecayReductionPerNecromancy = Config.Bind(
            "Per-skill bonuses", "Need decay reduction per Necromancy", 0.02f,
            "Fractional reduction to need decay for each Necromancy skill level (0.02 = 2%). Restart required.");

        var harmony = new Harmony(PluginGuid);
        try
        {
            harmony.PatchAll();
        }
        catch (Exception exception)
        {
            // PatchAll may have applied earlier patches before a later invalid target
            // fails. Undo them so a game update cannot leave a partial mod active.
            harmony.UnpatchSelf();
            Logger.LogError($"{PluginName} was safely disabled during initialization: {exception}");
            enabled = false;
            return;
        }

        Logger.LogInfo($"{PluginName} {PluginVersion} loaded: passive magic-school scaling is active.");
    }
}

internal static class SkillValues
{
    internal static int Get(Entity entity, Skill skill)
    {
        return entity.TryGetComponent<SkillsComponent>(out var skills)
            ? Math.Max(0, skills.GetSkillValue(skill))
            : 0;
    }
}

[HarmonyPatch(typeof(CharacterLevelUtils), nameof(CharacterLevelUtils.GetLevelBasedManaStat),
    new[] { typeof(Entity), typeof(int) })]
internal static class PyromancyManaPatch
{
    [HarmonyPostfix]
    private static void AddPyromancyMana(Entity __0, ref float __result)
    {
        __result += SkillValues.Get(__0, Skill.Pyromancy) * Plugin.ManaPerPyromancy.Value;
    }
}

[HarmonyPatch(typeof(CharacterLevelUtils), nameof(CharacterLevelUtils.GetLevelBasedHPStat),
    new[] { typeof(Entity), typeof(int) })]
internal static class GeomancyHitPointPatch
{
    [HarmonyPostfix]
    private static void AddGeomancyHitPoints(Entity __0, ref float __result)
    {
        __result += SkillValues.Get(__0, Skill.Geomancy) * Plugin.HitPointsPerGeomancy.Value;
    }
}

[HarmonyPatch(typeof(CharacterLevelUtils), nameof(CharacterLevelUtils.GetLevelBasedCombatStats),
    new[] { typeof(Entity), typeof(int) })]
internal static class DivinationAndManipulationCombatPatch
{
    [HarmonyPostfix]
    private static void AddMagicSchoolCombatStats(Entity __0, ref CombatStats __result)
    {
        Add(__result, CombatStat.DamageBonus,
            SkillValues.Get(__0, Skill.Divination) * Plugin.DamagePerDivination.Value);
        Add(__result, CombatStat.DodgeChance,
            SkillValues.Get(__0, Skill.Manipulation) * Plugin.DodgePerManipulation.Value);
        Add(__result, CombatStat.Regenerate,
            SkillValues.Get(__0, Skill.Hydrokinesis) * Plugin.RegenPerHydrokinesis.Value);
    }

    private static void Add(CombatStats stats, CombatStat stat, float amount)
    {
        if (amount == 0f)
        {
            return;
        }

        if (stats.Stats.TryGetValue(stat, out var existing))
        {
            stats.Stats[stat] = existing + amount;
        }
        else
        {
            stats.Stats[stat] = amount;
        }
    }
}

[HarmonyPatch(typeof(CharacterStatusUtils), nameof(CharacterStatusUtils.GetNeedRateModifier),
    new[] { typeof(NeedsType), typeof(Entity) })]
internal static class NecromancyNeedDecayPatch
{
    [HarmonyPostfix]
    private static void SlowNeedDecay(NeedsType __0, Entity __1, ref float __result)
    {
        // Conviction is a mood status rather than a decaying need, so it is deliberately
        // excluded. This applies the described benefit to ordinary mage needs only.
        if (__0 == NeedsType.None)
        {
            return;
        }

        var reduction = SkillValues.Get(__1, Skill.Necromancy) *
                        Plugin.NeedDecayReductionPerNecromancy.Value;
        __result *= Math.Max(0f, 1f - reduction);
    }
}
