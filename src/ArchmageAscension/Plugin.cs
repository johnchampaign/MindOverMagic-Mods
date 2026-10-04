using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
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
    public const string PluginVersion = "0.2.0";

    internal static ManualLogSource ModLog { get; private set; } = null!;
    internal static ConfigEntry<float> ManaPerPyromancy { get; private set; } = null!;
    internal static ConfigEntry<float> HitPointsPerGeomancy { get; private set; } = null!;
    internal static ConfigEntry<float> DamagePerDivination { get; private set; } = null!;
    internal static ConfigEntry<float> DodgePerManipulation { get; private set; } = null!;
    internal static ConfigEntry<float> RegenPerHydrokinesis { get; private set; } = null!;
    internal static ConfigEntry<float> NeedDecayReductionPerNecromancy { get; private set; } = null!;
    internal static ConfigEntry<float> ConvictionPerViturgy { get; private set; } = null!;
    internal static ConfigEntry<bool> StatBreakdownEnabled { get; private set; } = null!;
    internal static ConfigEntry<bool> RankStatusesEnabled { get; private set; } = null!;
    internal static ConfigEntry<int> AdeptSkillLevel { get; private set; } = null!;
    internal static ConfigEntry<int> ArchmageSkillLevel { get; private set; } = null!;
    internal static ConfigEntry<float> AirWalkSpeedMultiplier { get; private set; } = null!;
    internal static ConfigEntry<bool> AirArchmageGhostMovement { get; private set; } = null!;
    internal static ConfigEntry<float> FireCookingSpeedBonus { get; private set; } = null!;
    internal static ConfigEntry<bool> FireBattleCounterattack { get; private set; } = null!;
    internal static ConfigEntry<bool> DarkArchmageNeedsSated { get; private set; } = null!;
    internal static ConfigEntry<float> NatureArchmageConvictionToOthers { get; private set; } = null!;
    internal static ConfigEntry<float> EarthAdeptBattleArmour { get; private set; } = null!;
    internal static ConfigEntry<bool> EarthArchmageImmunity { get; private set; } = null!;
    internal static ConfigEntry<bool> WaterAdeptCleanse { get; private set; } = null!;
    internal static ConfigEntry<bool> LightningArchmageFreeSpells { get; private set; } = null!;
    internal static ConfigEntry<float> DarkAdeptLifesteal { get; private set; } = null!;
    internal static ConfigEntry<float> DarkAdeptHealOnEnemyDeath { get; private set; } = null!;
    internal static ConfigEntry<float> NatureAdeptLuxury { get; private set; } = null!;
    internal static ConfigEntry<bool> LightningAdeptManaVein { get; private set; } = null!;
    internal static ConfigEntry<bool> FireArchmageRenewal { get; private set; } = null!;
    internal static ConfigEntry<float> FireRenewalIntervalHours { get; private set; } = null!;
    internal static ConfigEntry<float> FireRenewalConviction { get; private set; } = null!;
    internal static ConfigEntry<int> DarkArchmageBountyAmount { get; private set; } = null!;
    internal static ConfigEntry<string> DarkArchmageBountyReagents { get; private set; } = null!;
    internal static ConfigEntry<bool> EarthArchmageNexusGateways { get; private set; } = null!;
    internal static ConfigEntry<int> CouncilRematchArchmages { get; private set; } = null!;
    internal static ConfigEntry<int> CouncilScoutingArchmages { get; private set; } = null!;
    internal static ConfigEntry<float> CouncilTravelCutPerArchmage { get; private set; } = null!;
    internal static ConfigEntry<int> CouncilRefiningArchmages { get; private set; } = null!;
    internal static ConfigEntry<float> CouncilRefiningMultiplier { get; private set; } = null!;
    internal static ConfigEntry<int> CouncilMendingArchmages { get; private set; } = null!;
    internal static ConfigEntry<float> CouncilMendingMultiplier { get; private set; } = null!;
    internal static ConfigEntry<int> CouncilSteadyMindsArchmages { get; private set; } = null!;
    internal static ConfigEntry<bool> CouncilEnabled { get; private set; } = null!;
    internal static ConfigEntry<int> CouncilResolveArchmages { get; private set; } = null!;
    internal static ConfigEntry<float> CouncilResolveConviction { get; private set; } = null!;
    internal static ConfigEntry<int> CouncilScholarshipArchmages { get; private set; } = null!;
    internal static ConfigEntry<float> CouncilScholarshipSpeedBonus { get; private set; } = null!;

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
        ConvictionPerViturgy = Config.Bind(
            "Per-skill bonuses", "Conviction per Viturgy", 2f,
            "Conviction target added for each Viturgy (Nature) skill level. It appears as " +
            "'Viturgy Attunement' in the Status & Conviction panel. Set to 0 to disable. Restart required.");
        StatBreakdownEnabled = Config.Bind(
            "Stat breakdown", "Enabled", true,
            "List each school's contribution in the game's stat hover text: the HP and Mana bars " +
            "on the selected-mage panel, and the stat icons on the Mage Sheet. Restart required.");

        RankStatusesEnabled = Config.Bind(
            "Rank statuses", "Enabled", true,
            "Show an Adept or Archmage status on each mage for every magic school they have " +
            "trained far enough. The statuses are informational badges; the per-skill bonuses " +
            "above apply regardless. Restart required.");
        AdeptSkillLevel = Config.Bind(
            "Rank statuses", "Adept skill level", 5,
            "Skill level at which a mage earns the Adept rank for a school. Set to 0 to " +
            "disable the Adept rank. Restart required.");
        ArchmageSkillLevel = Config.Bind(
            "Rank statuses", "Archmage skill level", 8,
            "Skill level at which a mage earns the Archmage rank for a school. The base game " +
            "caps skills at 8. Set to 0 to disable the Archmage rank. Restart required.");

        AirWalkSpeedMultiplier = Config.Bind(
            "Rank powers", "Air Adept walk speed multiplier", 2f,
            "Walking speed multiplier for Adepts and Archmages of Manipulation (Air). " +
            "1 disables. Restart required.");
        AirArchmageGhostMovement = Config.Bind(
            "Rank powers", "Air Archmage flight and phasing", true,
            "Archmages of Manipulation fly and pass through walls and floors, like the school's " +
            "founder. Restart required.");
        FireCookingSpeedBonus = Config.Bind(
            "Rank powers", "Fire Adept cooking speed bonus", 1f,
            "Extra cooking speed for Adepts and Archmages of Pyromancy (Fire): 1 = twice as fast. " +
            "0 disables. Restart required.");
        FireBattleCounterattack = Config.Bind(
            "Rank powers", "Fire Adept battle counterattack", true,
            "Adepts and Archmages of Pyromancy start every battle with Counterattack for two " +
            "rounds. Restart required.");
        DarkArchmageNeedsSated = Config.Bind(
            "Rank powers", "Dark Archmage needs stay sated", true,
            "Archmages of Necromancy (Dark) suffer no ordinary need decay. Conviction and Mana " +
            "are unaffected. Restart required.");
        NatureArchmageConvictionToOthers = Config.Bind(
            "Rank powers", "Nature Archmage Conviction to others", 5f,
            "Conviction target every other mage gains from each Archmage of Viturgy (Nature). " +
            "0 disables. Restart required.");
        EarthAdeptBattleArmour = Config.Bind(
            "Rank powers", "Earth Adept battle armour", 0.5f,
            "Armour granted to Adepts and Archmages of Geomancy (Earth) at the start of every " +
            "battle, as a fraction of Max HP (0.5 = 50%). 0 disables. Restart required.");
        EarthArchmageImmunity = Config.Bind(
            "Rank powers", "Earth Archmage immunity", true,
            "Archmages of Geomancy cannot be given harmful combat effects (the debuffs the " +
            "Sanctified terrain cleanses, such as Stunned, Blinded, Burns and Fear). Restart required.");
        WaterAdeptCleanse = Config.Bind(
            "Rank powers", "Water Adept cleanse", true,
            "Adepts and Archmages of Hydrokinesis (Water) shed harmful combat effects at the " +
            "start of every battle round. Restart required.");
        LightningArchmageFreeSpells = Config.Bind(
            "Rank powers", "Lightning Archmage free spells", true,
            "Archmages of Divination (Lightning) cast every spell, in battle or at school, " +
            "without spending mana. Restart required.");
        DarkAdeptLifesteal = Config.Bind(
            "Rank powers", "Dark Adept lifesteal", 0.1f,
            "Share of the damage their spells deal that Adepts and Archmages of Necromancy " +
            "(Dark) regain as HP (0.1 = 10%). 0 disables. Restart required.");
        DarkAdeptHealOnEnemyDeath = Config.Bind(
            "Rank powers", "Dark Adept heal on enemy death", 25f,
            "HP Adepts and Archmages of Necromancy regain whenever a foe falls in battle. " +
            "0 disables. Restart required.");
        NatureAdeptLuxury = Config.Bind(
            "Rank powers", "Nature Adept room Luxury", 2f,
            "Luxury added to every room while the school has at least one Adept or Archmage " +
            "of Viturgy (Nature). Does not stack. 0 disables. Restart required.");
        LightningAdeptManaVein = Config.Bind(
            "Rank powers", "Lightning Adept mana vein", true,
            "Adepts and Archmages of Divination (Lightning) start every battle standing on a " +
            "mana vein, which halves spell costs while they stay on it. Restart required.");
        FireArchmageRenewal = Config.Bind(
            "Rank powers", "Fire Archmage renewal", true,
            "Archmages of Pyromancy (Fire) periodically burn away one of their own traumas, or " +
            "failing that a scar, and are Renewed by Flame. Restart required.");
        FireRenewalIntervalHours = Config.Bind(
            "Rank powers", "Fire Archmage renewal interval (game hours)", 24f,
            "Game hours between two renewals for the same Archmage. Restart required.");
        FireRenewalConviction = Config.Bind(
            "Rank powers", "Fire Archmage renewal Conviction", 10f,
            "Conviction target granted by Renewed by Flame for a day. Restart required.");
        DarkArchmageBountyAmount = Config.Bind(
            "Rank powers", "Dark Archmage reagent bounty amount", 5,
            "Reagents each Archmage of Necromancy (Dark) gathers at midnight and at noon, " +
            "delivered to the school entrance. 0 disables. Restart required.");
        DarkArchmageBountyReagents = Config.Bind(
            "Rank powers", "Dark Archmage reagent bounty reagents",
            "Voidcap,MandrakeRoot,Bile,Brains,Viscera,ReapersCap",
            "Comma-separated item keys; each bounty picks one at random. Restart required.");
        EarthArchmageNexusGateways = Config.Bind(
            "Rank powers", "Earth Archmage Nexus Gateways", true,
            "While the school has an Archmage of Geomancy (Earth), Nexus Gateways can be built. " +
            "Gateways teleport to each other. Restart required.");

        CouncilEnabled = Config.Bind(
            "Archmage Council", "Enabled", true,
            "School-wide powers based on how many Archmage ranks the school holds in total. " +
            "The school's founder shows an 'Archmage Council' badge listing the powers in " +
            "force. Restart required.");
        CouncilResolveArchmages = Config.Bind(
            "Archmage Council", "Resolve: Archmage ranks needed", 3,
            "Archmage ranks the school needs before every mage gains the Resolve bonus. " +
            "0 disables. Restart required.");
        CouncilResolveConviction = Config.Bind(
            "Archmage Council", "Resolve: Conviction bonus", 10f,
            "Conviction target every mage gains from Resolve. Restart required.");
        CouncilScholarshipArchmages = Config.Bind(
            "Archmage Council", "Scholarship: Archmage ranks needed", 5,
            "Archmage ranks the school needs before teaching and learning speed up. " +
            "0 disables. Restart required.");
        CouncilScholarshipSpeedBonus = Config.Bind(
            "Archmage Council", "Scholarship: speed bonus", 1f,
            "Extra teaching and learning speed from Scholarship: 1 = twice as fast. Restart required.");
        CouncilRefiningArchmages = Config.Bind(
            "Archmage Council", "Refining: Archmage ranks needed", 2,
            "Archmage ranks the school needs before refineries hand back more. 0 disables. Restart required.");
        CouncilRefiningMultiplier = Config.Bind(
            "Archmage Council", "Refining: output multiplier", 2f,
            "Output multiplier for the Earth, Air, Fire and Dark refineries. Restart required.");
        CouncilMendingArchmages = Config.Bind(
            "Archmage Council", "Mending: Archmage ranks needed", 6,
            "Archmage ranks the school needs before wounds close faster. 0 disables. Restart required.");
        CouncilMendingMultiplier = Config.Bind(
            "Archmage Council", "Mending: healing speed multiplier", 3f,
            "How much faster wounds (trauma injuries) close. Restart required.");
        CouncilSteadyMindsArchmages = Config.Bind(
            "Archmage Council", "Steady Minds: Archmage ranks needed", 7,
            "Archmage ranks the school needs before mental breaks stop leaving mages At " +
            "Death's Door. 0 disables. Restart required.");
        CouncilRematchArchmages = Config.Bind(
            "Archmage Council", "Rematch: Archmage ranks needed", 1,
            "Archmage ranks the school needs before boss rematches pay the first-victory rewards, " +
            "relic included. 0 disables. Restart required.");
        CouncilScoutingArchmages = Config.Bind(
            "Archmage Council", "Open Ledgers: Archmage ranks needed", 4,
            "Archmage ranks the school needs before scouting a faction reveals every side quest " +
            "instead of two. 0 disables. Restart required.");
        CouncilTravelCutPerArchmage = Config.Bind(
            "Archmage Council", "Swift Travel: cut per Archmage rank", 0.1f,
            "Share of remaining quest travel removed per Archmage rank (0.1 = a tenth each; ten " +
            "ranks bring parties home at once). 0 disables. Restart required.");

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

/// <summary>
/// Rank badges: one informational, persistent character status per school rank. The
/// definitions live in Content/CharacterStatus/archmage_ranks.yaml and are injected into
/// the game's status catalog at load time, so no base-game file is touched.
/// </summary>
internal static class SchoolRanks
{
    internal const string AdeptRank = "Adept";
    internal const string ArchmageRank = "Archmage";

    internal static readonly Skill[] Schools =
    {
        Skill.Pyromancy, Skill.Geomancy, Skill.Divination,
        Skill.Manipulation, Skill.Hydrokinesis, Skill.Necromancy,
        // Viturgy is the game's "Nature" school.
        Skill.Viturgy
    };

    internal static readonly string[] Ranks = { AdeptRank, ArchmageRank };

    private static bool _unavailableLogged;

    internal static string ContentRoot =>
        Path.Combine(Path.GetDirectoryName(typeof(Plugin).Assembly.Location) ?? string.Empty, "Content");

    internal static string StatusKey(Skill school, string rank) =>
        $"ArchmageProgression_{school}_{rank}";

    internal static int Threshold(string rank) =>
        rank == ArchmageRank ? Plugin.ArchmageSkillLevel.Value : Plugin.AdeptSkillLevel.Value;

    /// <summary>
    /// The per-level benefit for a school as (per-level text, total text at the threshold).
    /// Necromancy is a fraction and is rendered as a percentage.
    /// </summary>
    internal static (string PerLevel, string MinBonus) BenefitText(Skill school, int threshold)
    {
        float perLevel = school switch
        {
            Skill.Pyromancy => Plugin.ManaPerPyromancy.Value,
            Skill.Geomancy => Plugin.HitPointsPerGeomancy.Value,
            Skill.Divination => Plugin.DamagePerDivination.Value,
            Skill.Manipulation => Plugin.DodgePerManipulation.Value,
            Skill.Hydrokinesis => Plugin.RegenPerHydrokinesis.Value,
            Skill.Necromancy => Plugin.NeedDecayReductionPerNecromancy.Value,
            Skill.Viturgy => Plugin.ConvictionPerViturgy.Value,
            _ => 0f
        };

        if (school == Skill.Necromancy)
        {
            return (Percent(perLevel), Percent(perLevel * threshold));
        }

        return (Number(perLevel), Number(perLevel * threshold));
    }

    internal static string Number(float value) =>
        value.ToString(Math.Abs(value % 1f) < 0.0005f ? "0" : "0.##", CultureInfo.InvariantCulture);

    private static string Percent(float value) =>
        (value * 100f).ToString(Math.Abs((value * 100f) % 1f) < 0.0005f ? "0" : "0.##",
            CultureInfo.InvariantCulture) + "%";

    internal static bool Enabled => Plugin.RankStatusesEnabled.Value &&
                                    (Plugin.AdeptSkillLevel.Value > 0 || Plugin.ArchmageSkillLevel.Value > 0);

    /// <summary>
    /// True when the live status catalog knows every rank status. Logs once if not, so a
    /// missing Content folder degrades to "no badges" rather than a flood of errors.
    /// </summary>
    /// <summary>Every status key the plugin applies; built once.</summary>
    private static readonly List<string> AllStatusKeys = BuildStatusKeys();

    private static List<string> BuildStatusKeys()
    {
        var keys = new List<string>();
        foreach (var school in Schools)
        {
            foreach (var rank in Ranks)
            {
                keys.Add(StatusKey(school, rank));
            }
        }

        for (var level = 1; level <= ViturgyAttunement.MaxLevel; level++)
        {
            keys.Add(ViturgyAttunement.StatusKey(level));
        }

        keys.AddRange(SchoolPowers.AllKeys());
        keys.Add(RankPowers.BulwarkKey);
        keys.Add(FireRenewal.RenewedKey);
        keys.Add(FireRenewal.CooldownKey);
        return keys;
    }

    internal static bool Available()
    {
        var catalog = DefinitionCatalog<CharacterStatusConfig>.Instance;
        if (catalog is null)
        {
            return false;
        }

        foreach (var key in AllStatusKeys)
        {
            if (!catalog.TryGetDefinitionFromStringKey(key, out _))
            {
                if (!_unavailableLogged)
                {
                    _unavailableLogged = true;
                    Plugin.ModLog.LogWarning(
                        $"Status '{key}' is not in the status catalog; rank badges, rank powers " +
                        $"and school-wide powers are disabled. Expected {ContentRoot}");
                }

                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Decide which rank, if any, a mage currently holds in a school based on trained
    /// (ungeared) skill level. Returns null when no rank applies.
    /// </summary>
    internal static string? DesiredRank(int skillLevel)
    {
        if (!Enabled)
        {
            return null;
        }

        var archmage = Plugin.ArchmageSkillLevel.Value;
        var adept = Plugin.AdeptSkillLevel.Value;
        if (archmage > 0 && skillLevel >= archmage)
        {
            return ArchmageRank;
        }

        if (adept > 0 && skillLevel >= adept)
        {
            return AdeptRank;
        }

        return null;
    }

    /// <summary>
    /// Bring one mage's rank statuses in line with its skills. Safe to call often: it only
    /// touches the status component when a badge actually changes.
    /// </summary>
    internal static void Refresh(Entity? entity)
    {
        if (entity is null || entity.IsDestroyed)
        {
            return;
        }

        if (!entity.HasComponent<StudentComponent>() && !entity.HasComponent<StaffComponent>())
        {
            return;
        }

        if (!entity.TryGetComponent<SkillsComponent>(out var skills) ||
            !entity.TryGetComponent<CharacterStatusComponent>(out var statuses))
        {
            return;
        }

        foreach (var school in Schools)
        {
            var desired = DesiredRank(skills.GetRawSkillValue(school));

            // Remove stale ranks first so the "earned the title" log entry for the new
            // rank is not immediately followed by a removal line.
            foreach (var rank in Ranks)
            {
                if (rank != desired)
                {
                    var key = StatusKey(school, rank);
                    if (CharacterStatusUtils.HasStatusKey(key, statuses))
                    {
                        CharacterStatusUtils.RemoveStatusKey(key, statuses);
                    }
                }
            }

            if (desired is not null)
            {
                var key = StatusKey(school, desired);
                if (!CharacterStatusUtils.HasStatusKey(key, statuses))
                {
                    var added = CharacterStatusUtils.AddStatusKey(key, statuses);
                    Plugin.ModLog.LogInfo(added
                        ? $"Granted {key} to {EntityUtils.GetDisplayName(entity)}."
                        : $"The game refused {key} for {EntityUtils.GetDisplayName(entity)}.");
                }
            }
        }

        RankPowers.EnsureGhostPathing(entity, statuses);
        ViturgyAttunement.Refresh(entity, statuses);
    }
}

/// <summary>
/// Adept and Archmage powers carried by the rank statuses themselves. Most are native
/// status fields written in the YAML (cooking speed, battle counterattack, ghost movement);
/// here they are tuned or stripped to match the config, and described in the tooltip.
/// Walking speed and need decay are code hooks that check for the rank statuses.
/// An Archmage holds only the Archmage status, so it carries the Adept power as well.
/// </summary>
internal static class RankPowers
{
    private const int GhostPathingType = 5;

    internal static bool HasRank(CharacterStatusComponent statuses, Skill school, bool archmageOnly) =>
        CharacterStatusUtils.HasStatusKey(SchoolRanks.StatusKey(school, SchoolRanks.ArchmageRank), statuses) ||
        (!archmageOnly &&
         CharacterStatusUtils.HasStatusKey(SchoolRanks.StatusKey(school, SchoolRanks.AdeptRank), statuses));

    internal const string BulwarkKey = "ArchmageProgression_EarthBulwark";

    private static string Percent(float fraction) => SchoolRanks.Number(fraction * 100f) + "%";

    internal static string Describe(Skill school, string rank)
    {
        var archmage = rank == SchoolRanks.ArchmageRank;
        var lines = new List<string>();
        switch (school)
        {
            case Skill.Manipulation:
                if (Plugin.AirWalkSpeedMultiplier.Value > 1f)
                {
                    lines.Add($"Walks {SchoolRanks.Number(Plugin.AirWalkSpeedMultiplier.Value)}x as fast outside combat.");
                }

                if (archmage && Plugin.AirArchmageGhostMovement.Value)
                {
                    lines.Add("Flies and passes through walls and floors.");
                }

                break;
            case Skill.Pyromancy:
                if (Plugin.FireCookingSpeedBonus.Value > 0f)
                {
                    lines.Add($"Cooks {Percent(Plugin.FireCookingSpeedBonus.Value)} faster.");
                }

                if (Plugin.FireBattleCounterattack.Value)
                {
                    lines.Add("Starts every battle with <slink=Counterattack> for two rounds.");
                }

                if (archmage && Plugin.FireArchmageRenewal.Value)
                {
                    lines.Add($"Every {SchoolRanks.Number(Plugin.FireRenewalIntervalHours.Value)} hours, burns away one " +
                              $"of their own traumas or scars and is Renewed by Flame " +
                              $"(+{SchoolRanks.Number(Plugin.FireRenewalConviction.Value)} <slink=Mood> target for a day).");
                }

                break;
            case Skill.Geomancy:
                if (Plugin.EarthAdeptBattleArmour.Value > 0f)
                {
                    lines.Add($"Starts every battle with <slink=TempHP> equal to {Percent(Plugin.EarthAdeptBattleArmour.Value)} of <slink=MaxHP>.");
                }

                if (archmage && Plugin.EarthArchmageImmunity.Value)
                {
                    lines.Add("Immune to harmful combat effects such as Stunned, Blinded, Burns and Fear.");
                }

                if (archmage && Plugin.EarthArchmageNexusGateways.Value)
                {
                    lines.Add("While the school has an Archmage of Earth, it can build Nexus Gateways.");
                }

                break;
            case Skill.Hydrokinesis:
                if (Plugin.WaterAdeptCleanse.Value)
                {
                    lines.Add("Sheds harmful combat effects at the start of every battle round.");
                }

                break;
            case Skill.Divination:
                if (Plugin.LightningAdeptManaVein.Value)
                {
                    lines.Add("Starts every battle standing on a mana vein.");
                }

                if (archmage && Plugin.LightningArchmageFreeSpells.Value)
                {
                    lines.Add("Casts every spell without spending mana, in battle or at school.");
                }

                break;
            case Skill.Necromancy:
                if (Plugin.DarkAdeptLifesteal.Value > 0f)
                {
                    lines.Add($"Regains {Percent(Plugin.DarkAdeptLifesteal.Value)} of the damage their spells deal as <slink=HP>.");
                }

                if (Plugin.DarkAdeptHealOnEnemyDeath.Value > 0f)
                {
                    lines.Add($"Regains {SchoolRanks.Number(Plugin.DarkAdeptHealOnEnemyDeath.Value)} <slink=HP> whenever a foe falls in battle.");
                }

                if (archmage && Plugin.DarkArchmageNeedsSated.Value)
                {
                    lines.Add("Ordinary needs no longer decay.");
                }

                if (archmage && Plugin.DarkArchmageBountyAmount.Value > 0)
                {
                    lines.Add($"At midnight and noon, gathers {Plugin.DarkArchmageBountyAmount.Value} of a random " +
                              "dark reagent, delivered to the school entrance.");
                }

                break;
            case Skill.Viturgy:
                if (Plugin.NatureAdeptLuxury.Value != 0f)
                {
                    lines.Add($"While the school has a Viturgy Adept or Archmage, every room gains " +
                              $"+{SchoolRanks.Number(Plugin.NatureAdeptLuxury.Value)} <slink=Luxury>.");
                }

                if (archmage && Plugin.NatureArchmageConvictionToOthers.Value > 0f)
                {
                    lines.Add($"Every other mage in the school gains " +
                              $"+{SchoolRanks.Number(Plugin.NatureArchmageConvictionToOthers.Value)} <slink=Mood> target.");
                }

                break;
        }

        return lines.Count == 0 ? string.Empty : "\n\nPowers: " + string.Join(" ", lines);
    }

    /// <summary>Fill the Earth battle-armour status from the config. False for any other status.</summary>
    internal static bool TryConfigureBulwark(CharacterStatusConfig definition)
    {
        if (definition.Id.Key != BulwarkKey)
        {
            return false;
        }

        definition.PercentArmorChangeOnAdd = Plugin.EarthAdeptBattleArmour.Value;
        if (definition.Description?.Text is { } text)
        {
            definition.Description.Text = text.Replace("{Percent}", Percent(Plugin.EarthAdeptBattleArmour.Value));
        }

        return true;
    }

    /// <summary>Tune or strip the native power fields to match the config.</summary>
    internal static void Configure(CharacterStatusConfig definition, Skill school, string rank)
    {
        if (school == Skill.Pyromancy)
        {
            if (Plugin.FireCookingSpeedBonus.Value <= 0f)
            {
                definition.WorkProgressModifiers = null;
            }
            else if (definition.WorkProgressModifiers is { } modifiers)
            {
                foreach (var modifier in modifiers)
                {
                    modifier.ModifierPercentage = Plugin.FireCookingSpeedBonus.Value;
                }
            }

            if (!Plugin.FireBattleCounterattack.Value)
            {
                definition.BattleChildStatuses = null;
            }
        }

        if (school == Skill.Manipulation && rank == SchoolRanks.ArchmageRank &&
            !Plugin.AirArchmageGhostMovement.Value)
        {
            definition.StatusEffects = null;
        }

        if (school == Skill.Geomancy && Plugin.EarthAdeptBattleArmour.Value <= 0f)
        {
            definition.BattleChildStatuses = null;
        }

        if (school == Skill.Necromancy && definition.FloatVariables is { } variables)
        {
            if (Plugin.DarkAdeptHealOnEnemyDeath.Value > 0f)
            {
                variables["HPGainOnEnemyDeath"] = Plugin.DarkAdeptHealOnEnemyDeath.Value;
            }
            else
            {
                variables.Remove("HPGainOnEnemyDeath");
            }
        }
    }

    /// <summary>
    /// The game attaches ghost pathing only when a GhostMovement status is first added, so a
    /// mage who already held the Archmage of Manipulation rank before this power existed
    /// would never fly. Attach it the same way the game does.
    /// </summary>
    internal static void EnsureGhostPathing(Entity entity, CharacterStatusComponent statuses)
    {
        if (!HasRank(statuses, Skill.Manipulation, archmageOnly: true))
        {
            // Losing the rank removes its status, and the game strips the flight itself.
            return;
        }

        if (!Plugin.AirArchmageGhostMovement.Value)
        {
            RemoveLeftoverGhostPathing(entity, statuses);
            return;
        }

        if (entity.HasComponent<PathingTypeOverrideComponent>(static c => (int)c.PathingType == GhostPathingType))
        {
            return;
        }

        var component = new PathingTypeOverrideComponent();
        component.PathingType = (PathingManager.PathingType)GhostPathingType;
        entity.AddComponent(component);
        EntityUtils.CancelCurrentPathing(entity);
        Simulation.Instance?.PathingManager?.RebuildPathEstimateForEntity(entity);
        Plugin.ModLog.LogInfo($"Gave {EntityUtils.GetDisplayName(entity)} Archmage of Manipulation flight.");
    }

    /// <summary>
    /// With flight switched off in the config, an Archmage of Manipulation who flew under an
    /// earlier session keeps the saved pathing override. Remove it the way the game does when
    /// a ghost-movement status ends, unless another status, or being a ghost, still grants it.
    /// </summary>
    private static void RemoveLeftoverGhostPathing(Entity entity, CharacterStatusComponent statuses)
    {
        if (entity.HasComponent<GhostComponent>() || entity.HasComponent<SchoolFounderComponent>() ||
            CharacterStatusUtils.HasStatusEffect(StatusEffect.GhostMovement, statuses, out CharacterStatus _) ||
            !entity.TryGetComponent<PathingTypeOverrideComponent>(
                static c => (int)c.PathingType == GhostPathingType, out var component))
        {
            return;
        }

        component.Destroy();
        EntityUtils.CancelCurrentPathing(entity);
        Simulation.Instance?.PathingManager?.RebuildPathEstimateForEntity(entity);
        if (EntityUtils.GetCurrentPathingCell(entity) is null &&
            entity.TryGetComponent<BehaviorStackComponent>(out var behaviors))
        {
            behaviors.CancelAllBehaviors();
        }

        Plugin.ModLog.LogInfo($"Removed Archmage of Manipulation flight from {EntityUtils.GetDisplayName(entity)} (disabled in config).");
    }
}

/// <summary>
/// Viturgy's per-level Conviction bias. Conviction targets are summed from the Mood
/// need-rate changes of a mage's statuses, so the bonus is one native status per skill
/// level whose TargetBias is set from the config at load. The Status &amp; Conviction panel
/// then lists it with its exact value, exactly like the base game's own modifiers.
/// </summary>
internal static class ViturgyAttunement
{
    /// <summary>Highest level with a definition; covers the skill cap plus gear bonuses.</summary>
    internal const int MaxLevel = 12;
    private const string KeyPrefix = "ArchmageProgression_ViturgyAttunement_";
    private static bool _clampLogged;

    internal static string StatusKey(int level) => KeyPrefix + level.ToString(CultureInfo.InvariantCulture);

    internal static bool Enabled => Plugin.ConvictionPerViturgy.Value > 0f;

    /// <summary>Total Conviction this mage gains from Viturgy, in Conviction points.</summary>
    internal static float Bonus(Entity entity) =>
        Enabled ? Level(entity) * Plugin.ConvictionPerViturgy.Value : 0f;

    private static int Level(Entity entity)
    {
        var level = SkillValues.Get(entity, Skill.Viturgy);
        if (level > MaxLevel)
        {
            if (!_clampLogged)
            {
                _clampLogged = true;
                Plugin.ModLog.LogWarning(
                    $"A Viturgy level of {level} exceeds the {MaxLevel} attunement levels defined; " +
                    $"the Conviction bonus is capped at level {MaxLevel}.");
            }

            return MaxLevel;
        }

        return level;
    }

    /// <summary>
    /// Fill one attunement definition from the config. Returns false for any other status.
    /// TargetBias uses the serialized scale, where 0.01 is one Conviction point.
    /// </summary>
    internal static bool TryConfigure(CharacterStatusConfig definition)
    {
        var key = definition.Id.Key ?? string.Empty;
        if (!key.StartsWith(KeyPrefix, StringComparison.Ordinal) ||
            !int.TryParse(key.Substring(KeyPrefix.Length), NumberStyles.Integer,
                CultureInfo.InvariantCulture, out var level))
        {
            return false;
        }

        var bonus = level * Plugin.ConvictionPerViturgy.Value;
        if (definition.NeedRateChanges is { } changes)
        {
            foreach (var change in changes)
            {
                if (change.NeedsType == NeedsType.Mood)
                {
                    change.TargetBias = bonus / 100f;
                }
            }
        }

        if (definition.Description?.Text is { } text)
        {
            definition.Description.Text = text
                .Replace("{Level}", level.ToString(CultureInfo.InvariantCulture))
                .Replace("{PerLevel}", SchoolRanks.Number(Plugin.ConvictionPerViturgy.Value))
                .Replace("{Bonus}", SchoolRanks.Number(bonus));
        }

        return true;
    }

    /// <summary>Keep exactly the attunement status matching the mage's Viturgy level.</summary>
    internal static void Refresh(Entity entity, CharacterStatusComponent statuses)
    {
        var desired = Enabled ? Level(entity) : 0;
        for (var level = 1; level <= MaxLevel; level++)
        {
            var key = StatusKey(level);
            var has = CharacterStatusUtils.HasStatusKey(key, statuses);
            if (level == desired && !has)
            {
                CharacterStatusUtils.AddStatusKey(key, statuses);
            }
            else if (level != desired && has)
            {
                CharacterStatusUtils.RemoveStatusKey(key, statuses);
            }
        }
    }
}

[HarmonyPatch(typeof(CharacterStatusConfigCatalog), nameof(CharacterStatusConfigCatalog.Load))]
internal static class RankStatusCatalogPatch
{
    [ThreadStatic] private static bool _loadingMod;

    /// <summary>
    /// Non-virtual call to the generic base loader. Loading the plugin's YAML through the
    /// patched <see cref="CharacterStatusConfigCatalog.Load"/> would run every other
    /// plugin's postfix against this temporary catalog, which can trip their once-only
    /// guards and leave their statuses out of the real catalog.
    /// </summary>
    private static readonly Action<DefinitionCatalog<CharacterStatusConfig>, string, YamlParserType>? BaseLoad =
        CreateBaseLoad();

    private static Action<DefinitionCatalog<CharacterStatusConfig>, string, YamlParserType>? CreateBaseLoad()
    {
        var method = AccessTools.Method(
            typeof(DefinitionCatalog<CharacterStatusConfig>), "Load",
            new[] { typeof(string), typeof(YamlParserType) });
        return method is null
            ? null
            : AccessTools.MethodDelegate<Action<DefinitionCatalog<CharacterStatusConfig>, string, YamlParserType>>(
                method, null, virtualCall: false);
    }

    [HarmonyPostfix]
    private static void InjectRankStatuses(CharacterStatusConfigCatalog __instance, YamlParserType __1)
    {
        if (_loadingMod)
        {
            return;
        }

        // Definitions are injected even when badges are disabled so saves that already
        // carry a rank status still resolve it; the sweep then removes those badges.
        // Keyed on content rather than a static flag so a rebuilt catalog is repopulated.
        if (__instance.TryGetDefinitionFromStringKey(
                SchoolRanks.StatusKey(Skill.Pyromancy, SchoolRanks.AdeptRank), out _))
        {
            return;
        }

        var root = SchoolRanks.ContentRoot;
        if (!Directory.Exists(root))
        {
            Plugin.ModLog.LogWarning($"Content directory not found, rank badges disabled: {root}");
            return;
        }

        if (BaseLoad is null)
        {
            Plugin.ModLog.LogError("Could not resolve the base definition loader; rank badges disabled.");
            return;
        }

        _loadingMod = true;
        try
        {
            var modCatalog = new CharacterStatusConfigCatalog();
            BaseLoad(modCatalog, root, __1);
            var injected = 0;
            foreach (var definition in modCatalog.AllDefinitions())
            {
                // Only this plugin's own definitions may enter the live catalog. Anything
                // else the loader produced is reported rather than risk shadowing a
                // base-game status.
                var key = definition?.Id.Key;
                if (key is null || !key.StartsWith("ArchmageProgression_", StringComparison.Ordinal))
                {
                    Plugin.ModLog.LogWarning(
                        $"Skipped an unexpected status definition from {root}: '{key ?? "<null>"}'.");
                    continue;
                }

                FillDescription(definition!);
                __instance.Add(definition!);
                injected++;
            }

            DefinitionCatalog<CharacterStatusConfig>.Instance = __instance;
            Plugin.ModLog.LogInfo($"Injected {injected} Archmage Progression statuses into the status catalog.");
        }
        catch (Exception exception)
        {
            Plugin.ModLog.LogError($"Failed to inject school rank statuses: {exception}");
        }
        finally
        {
            _loadingMod = false;
        }
    }

    /// <summary>
    /// Replace the {Threshold}, {PerLevel} and {MinBonus} tokens in a rank status's
    /// description with the values from this install's config, so the tooltip the player
    /// reads always matches what the plugin is actually granting.
    /// </summary>
    private static void FillDescription(CharacterStatusConfig definition)
    {
        var key = definition.Id.Key ?? string.Empty;
        if (ViturgyAttunement.TryConfigure(definition) || SchoolPowers.TryConfigure(definition) ||
            RankPowers.TryConfigureBulwark(definition) || FireRenewal.TryConfigure(definition))
        {
            return;
        }

        foreach (var school in SchoolRanks.Schools)
        {
            foreach (var rank in SchoolRanks.Ranks)
            {
                if (key != SchoolRanks.StatusKey(school, rank))
                {
                    continue;
                }

                var threshold = SchoolRanks.Threshold(rank);
                var (perLevel, minBonus) = SchoolRanks.BenefitText(school, threshold);
                if (definition.Description?.Text is { } text)
                {
                    definition.Description.Text = text
                        .Replace("{Threshold}", threshold.ToString(CultureInfo.InvariantCulture))
                        .Replace("{PerLevel}", perLevel)
                        .Replace("{MinBonus}", minBonus)
                        .Replace("{Powers}", RankPowers.Describe(school, rank));
                }

                RankPowers.Configure(definition, school, rank);
                return;
            }
        }
    }
}

/// <summary>
/// Periodic sweep. The game already re-evaluates its own "fully trained" status for every
/// student on a schedule; piggybacking on that scheduler keeps the rank badges correct
/// for existing saves, config changes, and any skill change that bypasses the hooks below.
/// It also strips badges from saves when the feature has been switched off, which is the
/// supported way to clean a save before uninstalling.
/// </summary>
[HarmonyPatch(typeof(UpdateMaxedSkillStatusSystem), nameof(UpdateMaxedSkillStatusSystem.Update))]
internal static class RankStatusSweepPatch
{
    private static readonly List<SkillsComponent> Scratch = new();
    private static readonly List<SchoolFounderComponent> FounderScratch = new();
    private static readonly List<Entity> Mages = new();
    private static readonly List<Entity> Founders = new();
    private static bool _firstRunLogged;
    private static bool _mageCountLogged;

    [HarmonyPostfix]
    private static void RefreshAllRanks(Simulation __0)
    {
        if (!_firstRunLogged)
        {
            // One line per session proving the sweep fires; rank grants log separately.
            _firstRunLogged = true;
            Plugin.ModLog.LogInfo(
                $"Rank sweep is running (simulation present: {__0?.ComponentManager is not null}, " +
                $"rank statuses available: {SchoolRanks.Available()}).");
        }

        if (__0?.ComponentManager is null || !SchoolRanks.Available())
        {
            return;
        }

        // Gather first: adding a status can attach components, and mutating the
        // component manager while enumerating it would be unsafe.
        Scratch.Clear();
        __0.ComponentManager.InSchoolGather(Scratch, static _ => true);
        if (!_mageCountLogged)
        {
            _mageCountLogged = true;
            var mages = 0;
            foreach (var skills in Scratch)
            {
                if (skills.Entity is { } e &&
                    (e.HasComponent<StudentComponent>() || e.HasComponent<StaffComponent>()))
                {
                    mages++;
                }
            }

            Plugin.ModLog.LogInfo(
                $"Rank sweep found {Scratch.Count} skilled entities in school, {mages} of them mages.");
        }

        Mages.Clear();
        foreach (var skills in Scratch)
        {
            try
            {
                SchoolRanks.Refresh(skills.Entity);
                if (skills.Entity is { IsDestroyed: false } entity &&
                    (entity.HasComponent<StudentComponent>() || entity.HasComponent<StaffComponent>()))
                {
                    Mages.Add(entity);
                }
            }
            catch (Exception exception)
            {
                Plugin.ModLog.LogError($"Failed to refresh rank statuses: {exception}");
            }
        }

        Scratch.Clear();
        try
        {
            FounderScratch.Clear();
            __0.ComponentManager.InSchoolGather(FounderScratch, static _ => true);
            Founders.Clear();
            foreach (var founder in FounderScratch)
            {
                if (founder.Entity is { IsDestroyed: false } entity)
                {
                    Founders.Add(entity);
                }
            }

            SchoolPowers.RefreshSchool(Mages, Founders);
        }
        catch (Exception exception)
        {
            Plugin.ModLog.LogError($"Failed to refresh school-wide powers: {exception}");
        }
        finally
        {
            FounderScratch.Clear();
            Founders.Clear();
            Mages.Clear();
        }
    }
}

/// <summary>
/// Immediate response: the moment a mage's trained skill level changes, re-check its ranks
/// so the badge and log entry appear with the level-up rather than at the next sweep.
/// </summary>
[HarmonyPatch(typeof(SkillsComponent), nameof(SkillsComponent.ModifySkillValue))]
internal static class RankStatusSkillChangePatch
{
    [HarmonyPostfix]
    private static void RefreshRanks(SkillsComponent __instance)
    {
        // Entities are also initialized through this path before the simulation exists
        // or before all of their components are attached; the sweep covers those.
        if (!SchoolRanks.Enabled || Simulation.Instance is null || !SchoolRanks.Available())
        {
            return;
        }

        try
        {
            SchoolRanks.Refresh(__instance.Entity);
        }
        catch (Exception exception)
        {
            Plugin.ModLog.LogError($"Failed to refresh rank statuses after a skill change: {exception}");
        }
    }
}

/// <summary>
/// Stat breakdown: adds each school's contribution to the game's own stat hover text, in
/// the same "{source}: +{amount}" form the game uses for traits and statuses. Covers the
/// HP and Mana bars on the selected-mage panel and the stat icons on the Mage Sheet.
/// </summary>
internal static class StatBreakdown
{
    private const string Source = "Archmage Progression";

    /// <summary>The school, per-level amount and stat label behind a hover keyword.</summary>
    private static bool TryGetSource(string? keyword, out Skill school, out float perLevel)
    {
        switch (keyword)
        {
            case "HP":
            case "MaxHP":
                school = Skill.Geomancy;
                perLevel = Plugin.HitPointsPerGeomancy.Value;
                return true;
            case "Mana":
            case "MaxMana":
                school = Skill.Pyromancy;
                perLevel = Plugin.ManaPerPyromancy.Value;
                return true;
            case "CombatStat_DamageBonus":
                school = Skill.Divination;
                perLevel = Plugin.DamagePerDivination.Value;
                return true;
            case "CombatStat_DodgeChance":
                school = Skill.Manipulation;
                perLevel = Plugin.DodgePerManipulation.Value;
                return true;
            default:
                school = Skill.None;
                perLevel = 0f;
                return false;
        }
    }

    /// <summary>
    /// One breakdown line such as "Earth 8 (Archmage Progression): +40", or null when the
    /// mage gains nothing from that school.
    /// </summary>
    internal static string? Line(Entity? entity, string? keyword)
    {
        if (!Plugin.StatBreakdownEnabled.Value || entity is null ||
            !TryGetSource(keyword, out var school, out var perLevel))
        {
            return null;
        }

        var level = SkillValues.Get(entity, school);
        var amount = level * perLevel;
        if (level <= 0 || amount == 0f)
        {
            return null;
        }

        var sign = amount > 0f ? "+" : string.Empty;
        return $"<slink=Skill_{school}> {level} ({Source}): {sign}{SchoolRanks.Number(amount)}";
    }

    /// <summary>
    /// Resolve a game UI method by name. UI code changes more often than simulation code, so
    /// a missing target only skips that one patch (Harmony honours Prepare returning false)
    /// instead of failing PatchAll and disabling the whole plugin.
    /// </summary>
    internal static MethodBase? Resolve(string typeAndMethod)
    {
        var parts = typeAndMethod.Split(':');
        MethodBase? method = null;
        try
        {
            // Name the View assembly so it is loaded on demand; plugins can initialise
            // before the game has touched any UI type.
            var type = AccessTools.TypeByName(parts[0]) ?? Type.GetType(parts[0] + ", View");
            method = type is null ? null : AccessTools.Method(type, parts[1]);
        }
        catch (Exception exception)
        {
            Plugin.ModLog.LogWarning($"Stat breakdown: lookup of {typeAndMethod} failed: {exception.Message}");
        }

        if (method is null)
        {
            Plugin.ModLog.LogWarning(
                $"Stat breakdown: could not find {typeAndMethod}; that hover text will not be extended.");
        }

        return method;
    }

    internal static void Append(ref string? text, string? line, bool separate)
    {
        if (line is null || text is null)
        {
            return;
        }

        text += (separate ? "\n\n" : "\n") + line;
    }
}

[HarmonyPatch]
internal static class HitPointBarTooltipPatch
{
    private static readonly MethodBase? Target = StatBreakdown.Resolve("View.GameHPWidget:HandleTooltip");

    private static bool Prepare() => Target is not null;

    private static MethodBase TargetMethod() => Target!;

    [HarmonyPostfix]
    private static void AddGeomancy(Entity __0, ref string? __result)
    {
        try
        {
            StatBreakdown.Append(ref __result, StatBreakdown.Line(__0, "HP"), separate: false);
        }
        catch (Exception exception)
        {
            Plugin.ModLog.LogError($"Failed to extend the HP tooltip: {exception}");
        }
    }
}

[HarmonyPatch]
internal static class ManaBarTooltipPatch
{
    private static readonly MethodBase? Target = StatBreakdown.Resolve("View.GameManaWidget:HandleTooltip");

    private static bool Prepare() => Target is not null;

    private static MethodBase TargetMethod() => Target!;

    [HarmonyPostfix]
    private static void AddPyromancy(Entity __0, ref string? __result)
    {
        try
        {
            StatBreakdown.Append(ref __result, StatBreakdown.Line(__0, "Mana"), separate: false);
        }
        catch (Exception exception)
        {
            Plugin.ModLog.LogError($"Failed to extend the Mana tooltip: {exception}");
        }
    }
}

/// <summary>
/// The game's per-mage keyword hover, used by the Mage Sheet stat icons. For HP, Mana and
/// Damage Bonus it already lists traits and statuses; Max HP, Max Mana and Dodge get a
/// blank-line separator so the mod's line does not run into the keyword description.
/// </summary>
[HarmonyPatch]
internal static class KeywordBreakdownPatch
{
    private static readonly MethodBase? Target =
        StatBreakdown.Resolve("View.GameTextLinkHandler:HandleEntitySpecificKeyword");

    private static bool Prepare() => Target is not null;

    private static MethodBase TargetMethod() => Target!;

    [HarmonyPostfix]
    private static void AddSchoolSource(string __0, Entity __1, ref string? __result)
    {
        try
        {
            var nativeBreakdown = __0 is "HP" or "Mana" or "CombatStat_DamageBonus";
            StatBreakdown.Append(ref __result, StatBreakdown.Line(__1, __0), separate: !nativeBreakdown);
        }
        catch (Exception exception)
        {
            Plugin.ModLog.LogError($"Failed to extend the '{__0}' breakdown: {exception}");
        }
    }
}

/// <summary>
/// School-wide powers recomputed by the sweep: Embrace of Nature (each Archmage of Viturgy
/// raises every other mage's Conviction) and the Archmage Council (powers unlocked by the
/// number of Archmage ranks in the school). Each is a native status, so Conviction effects
/// appear in the Status &amp; Conviction panel. The founder carries the council badge.
/// </summary>
internal static class SchoolPowers
{
    internal const int NatureEmbraceLevels = 20;
    internal const int CouncilBadgeLevels = 40;
    private const string EmbracePrefix = "ArchmageProgression_NatureEmbrace_";
    private const string BadgePrefix = "ArchmageProgression_CouncilBadge_";
    internal const string ResolveKey = "ArchmageProgression_CouncilEffect_Conviction";
    internal const string ScholarshipKey = "ArchmageProgression_CouncilEffect_Scholarship";

    private static int _lastCouncilCount = -1;

    /// <summary>The school's Archmage rank count from the latest sweep.</summary>
    internal static int CouncilCount { get; private set; }

    /// <summary>Archmages of Necromancy found by the latest sweep (reagent bounty recipients).</summary>
    internal static List<Entity> DarkArchmages { get; } = new();

    /// <summary>Whether the school has an Archmage of Geomancy (Nexus Gateways buildable).</summary>
    internal static bool EarthArchmagePresent { get; private set; }

    /// <summary>Whether the Nature Adept room Luxury bonus currently applies.</summary>
    internal static bool NatureLuxuryActive { get; private set; }

    internal static bool Unlocked(int needed) => Unlocked(CouncilCount, needed);

    private static string EmbraceKey(int count) => EmbracePrefix + count.ToString(CultureInfo.InvariantCulture);

    private static string BadgeKey(int count) => BadgePrefix + count.ToString(CultureInfo.InvariantCulture);

    internal static IEnumerable<string> AllKeys()
    {
        for (var count = 1; count <= NatureEmbraceLevels; count++)
        {
            yield return EmbraceKey(count);
        }

        yield return ResolveKey;
        yield return ScholarshipKey;
        for (var count = 1; count <= CouncilBadgeLevels; count++)
        {
            yield return BadgeKey(count);
        }
    }

    private static bool Unlocked(int count, int needed) =>
        Plugin.CouncilEnabled.Value && needed > 0 && count >= needed;

    private static bool ResolveActive(int count) =>
        Unlocked(count, Plugin.CouncilResolveArchmages.Value) && Plugin.CouncilResolveConviction.Value != 0f;

    private static bool ScholarshipActive(int count) =>
        Unlocked(count, Plugin.CouncilScholarshipArchmages.Value) && Plugin.CouncilScholarshipSpeedBonus.Value > 0f;

    private static bool TryParseLevel(string key, string prefix, out int level)
    {
        level = 0;
        return key.StartsWith(prefix, StringComparison.Ordinal) &&
               int.TryParse(key.Substring(prefix.Length), NumberStyles.Integer, CultureInfo.InvariantCulture,
                   out level);
    }

    private static void SetMood(CharacterStatusConfig definition, float conviction)
    {
        if (definition.NeedRateChanges is { } changes)
        {
            foreach (var change in changes)
            {
                if (change.NeedsType == NeedsType.Mood)
                {
                    change.TargetBias = conviction / 100f;
                }
            }
        }
    }

    private static void SetDescription(CharacterStatusConfig definition, Func<string, string> fill)
    {
        if (definition.Description?.Text is { } text)
        {
            definition.Description.Text = fill(text);
        }
    }

    /// <summary>The council powers as tooltip text for a school holding this many Archmage ranks.</summary>
    private static string CouncilPowersText(int count)
    {
        var lines = new List<(int Needed, string Text)>();

        void Add(string name, int needed, bool configured, string effect)
        {
            if (!configured || needed <= 0)
            {
                return;
            }

            lines.Add((needed, count >= needed
                ? $"In force ({needed}+): {name}. {effect}"
                : $"Needs {needed}: {name}. {effect}"));
        }

        Add("Resolve", Plugin.CouncilResolveArchmages.Value, Plugin.CouncilResolveConviction.Value != 0f,
            $"Every mage gains +{SchoolRanks.Number(Plugin.CouncilResolveConviction.Value)} <slink=Mood> target.");
        Add("Scholarship", Plugin.CouncilScholarshipArchmages.Value, Plugin.CouncilScholarshipSpeedBonus.Value > 0f,
            $"Teaching and learning are {SchoolRanks.Number(Plugin.CouncilScholarshipSpeedBonus.Value * 100f)}% faster.");
        Add("Refining", Plugin.CouncilRefiningArchmages.Value, Plugin.CouncilRefiningMultiplier.Value > 1f,
            $"Refineries hand back {SchoolRanks.Number(Plugin.CouncilRefiningMultiplier.Value)}x what they make.");
        Add("Mending", Plugin.CouncilMendingArchmages.Value, Plugin.CouncilMendingMultiplier.Value > 1f,
            $"Wounds close {SchoolRanks.Number(Plugin.CouncilMendingMultiplier.Value)}x as fast.");
        Add("Steady Minds", Plugin.CouncilSteadyMindsArchmages.Value, true,
            "A mental break no longer leaves a mage At Death's Door.");
        Add("Rematch Spoils", Plugin.CouncilRematchArchmages.Value, true,
            "Boss rematches pay what the first victory paid, relic included.");
        Add("Open Ledgers", Plugin.CouncilScoutingArchmages.Value, true,
            "Scouting a faction reveals every side quest, not just two.");
        if (Plugin.CouncilTravelCutPerArchmage.Value > 0f)
        {
            var full = (int)Math.Ceiling(1f / Plugin.CouncilTravelCutPerArchmage.Value - 0.0001f);
            lines.Add((0, $"Swift Travel: each Archmage rank cuts quest travel by " +
                          $"{SchoolRanks.Number(Plugin.CouncilTravelCutPerArchmage.Value * 100f)}%; now " +
                          $"{SchoolRanks.Number(SwiftTravel.Factor(count) * 100f)}% of normal" +
                          (count >= full ? ", parties return at once." : $", and at {full} parties return at once.")));
        }
        lines.Sort((a, b) => a.Needed.CompareTo(b.Needed));
        return lines.Count == 0 ? string.Empty : "\n\n" + string.Join("\n", lines.ConvertAll(line => line.Text));
    }

    /// <summary>Fill one school-wide definition from the config. False for any other status.</summary>
    internal static bool TryConfigure(CharacterStatusConfig definition)
    {
        var key = definition.Id.Key ?? string.Empty;
        if (TryParseLevel(key, EmbracePrefix, out var embrace))
        {
            var bonus = embrace * Plugin.NatureArchmageConvictionToOthers.Value;
            SetMood(definition, bonus);
            SetDescription(definition, text => text
                .Replace("{Count}", embrace.ToString(CultureInfo.InvariantCulture))
                .Replace("{Bonus}", SchoolRanks.Number(bonus)));
            return true;
        }

        if (key == ResolveKey)
        {
            SetMood(definition, Plugin.CouncilResolveConviction.Value);
            SetDescription(definition, text => text
                .Replace("{Bonus}", SchoolRanks.Number(Plugin.CouncilResolveConviction.Value)));
            return true;
        }

        if (key == ScholarshipKey)
        {
            var bonus = Plugin.CouncilScholarshipSpeedBonus.Value;
            definition.TeachBonusPct = bonus;
            if (definition.SkillProgressModifiers is { } skills)
            {
                foreach (var modifier in skills)
                {
                    modifier.ModifierAmount = bonus;
                }
            }

            if (definition.WorkProgressModifiers is { } work)
            {
                foreach (var modifier in work)
                {
                    modifier.ModifierPercentage = bonus;
                }
            }

            SetDescription(definition, text => text
                .Replace("{Percent}", SchoolRanks.Number(bonus * 100f) + "%"));
            return true;
        }

        if (TryParseLevel(key, BadgePrefix, out var badge))
        {
            var shown = badge >= CouncilBadgeLevels
                ? $"{CouncilBadgeLevels} or more"
                : badge.ToString(CultureInfo.InvariantCulture);
            SetDescription(definition, text => text
                .Replace("{Count}", shown)
                .Replace("{Powers}", CouncilPowersText(badge)));
            return true;
        }

        return false;
    }

    /// <summary>Keep exactly one status of a numbered family, or none when level is 0.</summary>
    private static void SetLevel(CharacterStatusComponent statuses, Func<int, string> keyFor, int levels, int level)
    {
        for (var candidate = 1; candidate <= levels; candidate++)
        {
            var key = keyFor(candidate);
            var has = CharacterStatusUtils.HasStatusKey(key, statuses);
            if (candidate == level && !has)
            {
                CharacterStatusUtils.AddStatusKey(key, statuses);
            }
            else if (candidate != level && has)
            {
                CharacterStatusUtils.RemoveStatusKey(key, statuses);
            }
        }
    }

    private static void SetPresent(CharacterStatusComponent statuses, string key, bool present)
    {
        var has = CharacterStatusUtils.HasStatusKey(key, statuses);
        if (present && !has)
        {
            CharacterStatusUtils.AddStatusKey(key, statuses);
        }
        else if (!present && has)
        {
            CharacterStatusUtils.RemoveStatusKey(key, statuses);
        }
    }

    /// <summary>Recompute every school-wide power from the mages' current ranks.</summary>
    internal static void RefreshSchool(List<Entity> mages, List<Entity> founders)
    {
        var councilCount = 0;
        var natureArchmages = 0;
        var natureAdepts = 0;
        var earthArchmage = false;
        DarkArchmages.Clear();
        foreach (var mage in mages)
        {
            if (!mage.TryGetComponent<CharacterStatusComponent>(out var statuses))
            {
                continue;
            }

            if (RankPowers.HasRank(statuses, Skill.Viturgy, archmageOnly: false))
            {
                natureAdepts++;
            }

            if (RankPowers.HasRank(statuses, Skill.Necromancy, archmageOnly: true))
            {
                DarkArchmages.Add(mage);
            }

            earthArchmage |= RankPowers.HasRank(statuses, Skill.Geomancy, archmageOnly: true);
            FireRenewal.TryRenew(mage, statuses);

            foreach (var school in SchoolRanks.Schools)
            {
                if (CharacterStatusUtils.HasStatusKey(SchoolRanks.StatusKey(school, SchoolRanks.ArchmageRank), statuses))
                {
                    councilCount++;
                    if (school == Skill.Viturgy)
                    {
                        natureArchmages++;
                    }
                }
            }
        }

        if (councilCount != _lastCouncilCount)
        {
            Plugin.ModLog.LogInfo(
                $"Archmage Council: the school holds {councilCount} Archmage rank(s), " +
                $"{natureArchmages} of them in Viturgy.");
            _lastCouncilCount = councilCount;
        }

        CouncilCount = councilCount;
        EarthArchmagePresent = earthArchmage;
        if (Simulation.Instance is { } current)
        {
            SwiftTravel.Update(current, councilCount);
        }

        var luxury = Plugin.NatureAdeptLuxury.Value != 0f && natureAdepts > 0;
        if (luxury != NatureLuxuryActive)
        {
            NatureLuxuryActive = luxury;
            Plugin.ModLog.LogInfo(luxury
                ? "Nature Adept Luxury bonus is now active; recalculating rooms."
                : "Nature Adept Luxury bonus is no longer active; recalculating rooms.");
            if (Simulation.Instance is { } simulation)
            {
                UpdateRoomDataSystem.FullUpdate(simulation, false);
            }
        }

        var natureBonus = Plugin.NatureArchmageConvictionToOthers.Value > 0f;
        var resolve = ResolveActive(councilCount);
        var scholarship = ScholarshipActive(councilCount);
        foreach (var mage in mages)
        {
            if (!mage.TryGetComponent<CharacterStatusComponent>(out var statuses))
            {
                continue;
            }

            var others = natureArchmages -
                         (RankPowers.HasRank(statuses, Skill.Viturgy, archmageOnly: true) ? 1 : 0);
            SetLevel(statuses, EmbraceKey, NatureEmbraceLevels,
                natureBonus ? Math.Min(Math.Max(others, 0), NatureEmbraceLevels) : 0);
            SetPresent(statuses, ResolveKey, resolve);
            SetPresent(statuses, ScholarshipKey, scholarship);
        }

        var badge = Plugin.CouncilEnabled.Value ? Math.Min(councilCount, CouncilBadgeLevels) : 0;
        foreach (var founder in founders)
        {
            if (founder.TryGetComponent<CharacterStatusComponent>(out var statuses))
            {
                SetLevel(statuses, BadgeKey, CouncilBadgeLevels, badge);
            }
        }
    }
}

/// <summary>Air Adept and Archmage: faster walking, applied where the game computes it.</summary>
[HarmonyPatch(typeof(SpeedComponent), nameof(SpeedComponent.GetDistancePerGameMinute))]
internal static class AirWalkSpeedPatch
{
    private static bool Prepare() => Math.Abs(Plugin.AirWalkSpeedMultiplier.Value - 1f) > 0.0001f;

    [HarmonyPostfix]
    private static void WalkFaster(SpeedComponent __instance, ref float __result)
    {
        if (__result <= 0f || __instance.Entity is not { } entity ||
            !entity.TryGetComponent<CharacterStatusComponent>(out var statuses) ||
            !RankPowers.HasRank(statuses, Skill.Manipulation, archmageOnly: false))
        {
            return;
        }

        __result *= Plugin.AirWalkSpeedMultiplier.Value;
    }
}

/// <summary>
/// Necromancy slows ordinary need decay, and an Archmage of Necromancy stops it. The game
/// subtracts decay inside its need update with no multiplier to hook, so this records each
/// affected need just before the update and scales back whatever the update removed.
/// Conviction (a target-style need) and Mana (mirrored from the mana pool) are excluded.
/// </summary>
[HarmonyPatch]
internal static class NecromancyNeedDecayPatch
{
    private static readonly MethodBase? Target = Resolve();
    private static readonly List<NeedsComponent> Scratch = new();
    private static readonly List<(NeedsComponent Need, float Before, float Reduction)> Snapshot = new();
    private static readonly Dictionary<Entity, float> ReductionCache = new();

    private static MethodBase? Resolve()
    {
        var update = AccessTools.Method(typeof(UpdateNeedsSystem), "Update");
        var moveNext = update is null ? null : AccessTools.EnumeratorMoveNext(update);
        if (moveNext is null)
        {
            Plugin.ModLog.LogWarning("Could not find the need update; Necromancy need decay bonuses are inactive.");
        }

        return moveNext;
    }

    private static bool Prepare() => Target is not null;

    private static MethodBase TargetMethod() => Target!;

    private static bool IsOrdinary(NeedsType type) =>
        type is not (NeedsType.None or NeedsType.Mood or NeedsType.Mana or NeedsType.Count) &&
        NeedsConfig.GetNeedValueStyle(type) == NeedValueStyle.Decay;

    private static float Reduction(Entity entity)
    {
        if (ReductionCache.TryGetValue(entity, out var cached))
        {
            return cached;
        }

        float reduction;
        if (Plugin.DarkArchmageNeedsSated.Value &&
            entity.TryGetComponent<CharacterStatusComponent>(out var statuses) &&
            RankPowers.HasRank(statuses, Skill.Necromancy, archmageOnly: true))
        {
            reduction = 1f;
        }
        else
        {
            reduction = Math.Min(1f, Math.Max(0f,
                SkillValues.Get(entity, Skill.Necromancy) * Plugin.NeedDecayReductionPerNecromancy.Value));
        }

        ReductionCache[entity] = reduction;
        return reduction;
    }

    [HarmonyPrefix]
    private static void RecordNeeds()
    {
        Snapshot.Clear();
        if (Plugin.NeedDecayReductionPerNecromancy.Value <= 0f && !Plugin.DarkArchmageNeedsSated.Value)
        {
            return;
        }

        try
        {
            if (Simulation.Instance?.ComponentManager is not { } components)
            {
                return;
            }

            Scratch.Clear();
            ReductionCache.Clear();
            components.InSchoolGather(Scratch, static need => need.Config is { } config && IsOrdinary(config.NeedsType));
            foreach (var need in Scratch)
            {
                if (need.Entity is not { } entity)
                {
                    continue;
                }

                var reduction = Reduction(entity);
                if (reduction > 0f)
                {
                    Snapshot.Add((need, need.Value, reduction));
                }
            }
        }
        catch (Exception exception)
        {
            Snapshot.Clear();
            Plugin.ModLog.LogError($"Failed to record needs for Necromancy decay: {exception}");
        }
        finally
        {
            Scratch.Clear();
            ReductionCache.Clear();
        }
    }

    [HarmonyPostfix]
    private static void RestoreDecay()
    {
        try
        {
            foreach (var (need, before, reduction) in Snapshot)
            {
                if (need.IsDestroyed)
                {
                    continue;
                }

                var after = need.Value;
                if (after < before)
                {
                    need.Value = before - (before - after) * (1f - reduction);
                }
            }
        }
        catch (Exception exception)
        {
            Plugin.ModLog.LogError($"Failed to apply Necromancy need decay: {exception}");
        }
        finally
        {
            Snapshot.Clear();
        }
    }
}

/// <summary>
/// The game's own notion of a harmful combat effect: a non-beneficial status that the
/// "Sanctified" terrain cleanses (it lists Terrain_Sanctified in its NegConditions). This
/// covers enemy debuffs such as Stunned, Blinded, Burns, Fear and Soaked while leaving
/// ranks, fleeing, need levels, sleep, injuries and knock-outs alone.
/// </summary>
internal static class HarmfulStatus
{
    private static DefId<CharacterStatusConfig>? _sanctified;

    internal static bool Is(CharacterStatusConfig? config)
    {
        if (config is null || config.Beneficial || config.NegConditions is not { Count: > 0 } conditions)
        {
            return false;
        }

        if (_sanctified is null)
        {
            if (DefinitionCatalog<CharacterStatusConfig>.Instance is not { } catalog ||
                !catalog.TryGetDefId("Terrain_Sanctified", out var id))
            {
                return false;
            }

            _sanctified = id;
        }

        return conditions.Contains(_sanctified.Value);
    }
}

/// <summary>Earth Archmage: harmful combat effects cannot be applied.</summary>
[HarmonyPatch(typeof(CharacterStatusUtils), nameof(CharacterStatusUtils.IsStatusAllowed))]
internal static class EarthImmunityPatch
{
    private static bool Prepare() => Plugin.EarthArchmageImmunity.Value;

    [HarmonyPostfix]
    private static void BlockHarmful(CharacterStatusConfig __0, CharacterStatusComponent __1, ref bool __result)
    {
        if (__result && __1 is not null && HarmfulStatus.Is(__0) &&
            RankPowers.HasRank(__1, Skill.Geomancy, archmageOnly: true))
        {
            __result = false;
        }
    }
}

/// <summary>Water Adept: harmful combat effects are cleansed at the start of every round.</summary>
[HarmonyPatch(typeof(BattleUtils), nameof(BattleUtils.OnRoundStart))]
internal static class WaterCleansePatch
{
    private static readonly List<DefId<CharacterStatusConfig>> ToRemove = new();

    private static bool Prepare() => Plugin.WaterAdeptCleanse.Value;

    [HarmonyPostfix]
    private static void Cleanse(BattleData __0)
    {
        if (__0?.ActiveParty is not { } party)
        {
            return;
        }

        try
        {
            foreach (var member in party)
            {
                if (member?.Entity is not { } entity ||
                    !entity.TryGetComponent<CharacterStatusComponent>(out var statuses) ||
                    !RankPowers.HasRank(statuses, Skill.Hydrokinesis, archmageOnly: false))
                {
                    continue;
                }

                ToRemove.Clear();
                foreach (var status in statuses.Statuses.Values)
                {
                    if (HarmfulStatus.Is(status.Config))
                    {
                        ToRemove.Add(status.StatusId);
                    }
                }

                foreach (var id in ToRemove)
                {
                    CharacterStatusUtils.RemoveStatus(id, statuses);
                }
            }
        }
        catch (Exception exception)
        {
            Plugin.ModLog.LogError($"Water cleanse failed: {exception}");
        }
        finally
        {
            ToRemove.Clear();
        }
    }
}

/// <summary>
/// Lightning Archmage: spells cost no mana. Payment multiplies by the cost modifier after
/// adding the flat cost, so both are zeroed; the affordability check skips the modifier for
/// percentage-cost spells, so it is forced to succeed as well.
/// </summary>
internal static class FreeSpells
{
    internal static bool Applies(Entity? caster) =>
        Plugin.LightningArchmageFreeSpells.Value && caster is not null &&
        caster.TryGetComponent<CharacterStatusComponent>(out var statuses) &&
        RankPowers.HasRank(statuses, Skill.Divination, archmageOnly: true);
}

[HarmonyPatch(typeof(CharacterStatusUtils), nameof(CharacterStatusUtils.GetManaCostModifier))]
internal static class FreeSpellModifierPatch
{
    private static bool Prepare() => Plugin.LightningArchmageFreeSpells.Value;

    [HarmonyPostfix]
    private static void NoCost(Entity __0, ref float __result)
    {
        if (FreeSpells.Applies(__0))
        {
            __result = 0f;
        }
    }
}

[HarmonyPatch(typeof(CharacterStatusUtils), nameof(CharacterStatusUtils.GetExtraFlatManaCost))]
internal static class FreeSpellFlatCostPatch
{
    private static bool Prepare() => Plugin.LightningArchmageFreeSpells.Value;

    [HarmonyPostfix]
    private static void NoCost(Entity __0, ref float __result)
    {
        if (FreeSpells.Applies(__0))
        {
            __result = 0f;
        }
    }
}

[HarmonyPatch(typeof(SpellUtils), nameof(SpellUtils.HasEnoughManaToCast))]
internal static class FreeSpellAffordPatch
{
    private static bool Prepare() => Plugin.LightningArchmageFreeSpells.Value;

    [HarmonyPostfix]
    private static void Afford(Entity __0, ref bool __result)
    {
        if (!__result && FreeSpells.Applies(__0))
        {
            __result = true;
        }
    }
}

/// <summary>
/// Dark Adept lifesteal: heals the caster a share of the damage each of its spells deals,
/// through the game's own leech healing so logs and floating numbers match native leech.
/// Healing on enemy deaths is native (HPGainOnEnemyDeath on the rank status).
/// </summary>
[HarmonyPatch(typeof(DoDamageComponent), "HandlePostDamage")]
internal static class DarkLifestealPatch
{
    private static bool Prepare() => Plugin.DarkAdeptLifesteal.Value > 0f;

    [HarmonyPostfix]
    private static void Leech(DoDamageComponent __instance)
    {
        try
        {
            if (__instance?.ResultsPerTarget is not { Count: > 0 } results ||
                __instance.Entity is not { } spell ||
                !spell.TryGetComponent<CastByComponent>(out var castBy) ||
                castBy.CastByEntity is not { } caster ||
                !caster.TryGetComponent<CharacterStatusComponent>(out var statuses) ||
                !RankPowers.HasRank(statuses, Skill.Necromancy, archmageOnly: false) ||
                !caster.TryGetComponent<HitPointComponent>(out var hitPoints))
            {
                return;
            }

            var dealt = 0f;
            foreach (var pair in results)
            {
                if (pair.Key != caster && pair.Value is { } result && result.DamageDealt > 0f)
                {
                    dealt += result.DamageDealt;
                }
            }

            var heal = dealt * Plugin.DarkAdeptLifesteal.Value;
            if (heal > 0f)
            {
                hitPoints.ModifyHPFromLeech(heal, caster, spell);
            }
        }
        catch (Exception exception)
        {
            Plugin.ModLog.LogError($"Dark lifesteal failed: {exception}");
        }
    }
}

/// <summary>
/// Nature Adept: every room gains Luxury while the school has an Adept or Archmage of
/// Viturgy. Added to the room's total at the end of the game's own recalculation, the same
/// place the game folds in its display-affinity Luxury bonus, so room tiers, Luxury
/// statuses, bedroom quests and the room UI all see it.
/// </summary>
[HarmonyPatch]
internal static class NatureLuxuryPatch
{
    private static readonly MethodBase? Target = Resolve(out _state, out _room, out _roomData);
    private static AccessTools.FieldRef<object, int>? _state;
    private static AccessTools.FieldRef<object, RoomComponent>? _room;
    private static AccessTools.FieldRef<object, Room>? _roomData;

    private static MethodBase? Resolve(
        out AccessTools.FieldRef<object, int>? state,
        out AccessTools.FieldRef<object, RoomComponent>? room,
        out AccessTools.FieldRef<object, Room>? roomData)
    {
        state = null;
        room = null;
        roomData = null;
        try
        {
            var method = AccessTools.Method(typeof(RoomDataUtils), "ReCalculateAllRoomData");
            var moveNext = method is null ? null : AccessTools.EnumeratorMoveNext(method);
            var iterator = moveNext?.DeclaringType;
            if (iterator is null)
            {
                Plugin.ModLog.LogWarning("Could not find the room recalculation; the Nature Luxury bonus is inactive.");
                return null;
            }

            FieldInfo? Find(Type type) => Array.Find(
                iterator.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
                field => field.FieldType == type);

            var stateField = AccessTools.Field(iterator, "<>1__state");
            var roomField = Find(typeof(RoomComponent));
            var roomDataField = Find(typeof(Room));
            if (stateField is null || roomField is null || roomDataField is null)
            {
                Plugin.ModLog.LogWarning("Room recalculation layout changed; the Nature Luxury bonus is inactive.");
                return null;
            }

            state = AccessTools.FieldRefAccess<int>(iterator, stateField.Name);
            room = AccessTools.FieldRefAccess<RoomComponent>(iterator, roomField.Name);
            roomData = AccessTools.FieldRefAccess<Room>(iterator, roomDataField.Name);
            return moveNext;
        }
        catch (Exception exception)
        {
            Plugin.ModLog.LogWarning($"Nature Luxury bonus is inactive: {exception.Message}");
            return null;
        }
    }

    private static bool Prepare() => Plugin.NatureAdeptLuxury.Value != 0f && Target is not null;

    private static MethodBase TargetMethod() => Target!;

    [HarmonyPrefix]
    private static void RecordState(object __instance, out int __state) => __state = _state!(__instance);

    [HarmonyPostfix]
    private static void AddLuxury(object __instance, bool __result, int __state)
    {
        // Only once, when a recalculation that actually started finishes. The early exit for
        // a vanished room returns before the luxury reset and leaves roomData null.
        if (__result || __state == -1 || !SchoolPowers.NatureLuxuryActive || _roomData!(__instance) is null ||
            _room!(__instance) is not { } room)
        {
            return;
        }

        room.TotalLuxuryValue += Plugin.NatureAdeptLuxury.Value;
    }
}

/// <summary>Council: refineries hand back more of what they make.</summary>
[HarmonyPatch(typeof(CraftErrand), "CreateRecipeOutput")]
internal static class CouncilRefiningPatch
{
    private static readonly HashSet<string> Refineries = new(StringComparer.Ordinal)
    {
        "EarthRefinery", "AirRefinery", "AirRefinery_Deprecated", "FireRefinery", "MiddenHeap"
    };

    private static bool Prepare() =>
        Plugin.CouncilRefiningArchmages.Value > 0 && Plugin.CouncilRefiningMultiplier.Value > 1f;

    [HarmonyPrefix]
    private static void MoreOutput(CraftErrand __instance, ref int __0)
    {
        if (__0 <= 0 || !SchoolPowers.Unlocked(Plugin.CouncilRefiningArchmages.Value) ||
            __instance?.Entity?.EntityKey.Key is not { } station || !Refineries.Contains(station))
        {
            return;
        }

        __0 = (int)Math.Round(__0 * Plugin.CouncilRefiningMultiplier.Value, MidpointRounding.AwayFromZero);
    }
}

/// <summary>
/// Council: wounds close faster. Wounds are the injury catalog's trauma statuses, which the
/// generic status-expiration system counts down by dt each tick. Taking extra time off just
/// before it runs keeps expiry, the scar that follows and all logging native.
/// </summary>
[HarmonyPatch(typeof(UpdateCharacterStatusExpirationSystem), nameof(UpdateCharacterStatusExpirationSystem.Update))]
internal static class CouncilMendingPatch
{
    private static readonly List<CharacterStatusComponent> Scratch = new();

    private static bool Prepare() =>
        Plugin.CouncilMendingArchmages.Value > 0 && Plugin.CouncilMendingMultiplier.Value > 1f;

    [HarmonyPrefix]
    private static void HealFaster(Simulation __0, float __1)
    {
        if (__1 <= 0f || !SchoolPowers.Unlocked(Plugin.CouncilMendingArchmages.Value) ||
            __0?.Configs?.InjuryCatalog?.TraumaStatuses is not { Count: > 0 } wounds ||
            __0.ComponentManager is not { } components)
        {
            return;
        }

        var extra = (Plugin.CouncilMendingMultiplier.Value - 1f) * __1;
        try
        {
            Scratch.Clear();
            components.InSchoolGather(Scratch, static c => c.Entity is { } e &&
                (e.HasComponent<StudentComponent>() || e.HasComponent<StaffComponent>()));
            foreach (var statuses in Scratch)
            {
                foreach (var status in statuses.GetTimedStatuses())
                {
                    if (wounds.Contains(status.StatusId))
                    {
                        status.TimeRemaining -= extra;
                    }
                }
            }
        }
        catch (Exception exception)
        {
            Plugin.ModLog.LogError($"Council mending failed: {exception}");
        }
        finally
        {
            Scratch.Clear();
        }
    }
}

/// <summary>
/// Council: a mental break no longer leaves the mage At Death's Door. Only knock-outs
/// caused by a break are skipped; combat and zero-HP knock-outs are untouched. The mood
/// recovery the game grants on reviving a broken mage is granted straight away instead.
/// </summary>
[HarmonyPatch(typeof(InjuryUtils), nameof(InjuryUtils.ApplyKnockOut))]
internal static class CouncilSteadyMindsPatch
{
    private static bool Prepare() => Plugin.CouncilSteadyMindsArchmages.Value > 0;

    [HarmonyPrefix]
    private static bool SkipBreakKnockOut(Entity __0, KnockedOutComponent.KnockOutCause __1)
    {
        if (__1 != KnockedOutComponent.KnockOutCause.Break || __0 is null ||
            !SchoolPowers.Unlocked(Plugin.CouncilSteadyMindsArchmages.Value))
        {
            return true;
        }

        try
        {
            CharacterStatusUtils.AddStatusKey("HadBreak", __0, null);
            CharacterStatusUtils.AddStatusKey("PostBreakStatusClear", __0, null);
            Plugin.ModLog.LogInfo($"Archmage Council spared {EntityUtils.GetDisplayName(__0)} from At Death's Door after a break.");
        }
        catch (Exception exception)
        {
            Plugin.ModLog.LogError($"Steady Minds recovery statuses failed: {exception}");
        }

        return false;
    }
}

/// <summary>
/// Lightning Adept: starts every battle standing on a mana vein. Rather than faking the
/// buff, the plugin puts a real Terrain_ManaVein on the mage's starting slot right after
/// the game sets up the friendly side's terrain, so the buff, its turn-by-turn refresh,
/// the slot visual and the cleanup at battle end are all native.
/// </summary>
[HarmonyPatch(typeof(BattleUtils), "SpawnFriendlySide")]
internal static class LightningManaVeinPatch
{
    private static DefId<CharacterStatusConfig>? _vein;

    private static bool Prepare() => Plugin.LightningAdeptManaVein.Value;

    [HarmonyPostfix]
    private static void PlaceVeins(BattleData __0)
    {
        if (__0?.ActiveParty is not { } party || __0.CombatSlotModifiers is not { } slots)
        {
            return;
        }

        try
        {
            if (_vein is null)
            {
                if (DefinitionCatalog<CharacterStatusConfig>.Instance is not { } catalog ||
                    !catalog.TryGetDefId("Terrain_ManaVein", out var id))
                {
                    return;
                }

                _vein = id;
            }

            foreach (var member in party)
            {
                if (member?.Entity is not { } entity ||
                    !entity.TryGetComponent<CharacterStatusComponent>(out var statuses) ||
                    !RankPowers.HasRank(statuses, Skill.Divination, archmageOnly: false))
                {
                    continue;
                }

                var slot = member.CombatSlotIndex;
                if (slot < 0 || slots.Exists(m => m.Side == CombatSide.Left && m.SlotIndex == slot &&
                                                  m.Status.GetDefinition() is { PermanentTerrain: true }))
                {
                    continue;
                }

                slots.RemoveAll(m => m.Side == CombatSide.Left && m.SlotIndex == slot);
                slots.Add(new BattleData.CombatSlotModifier
                {
                    Side = CombatSide.Left,
                    SlotIndex = slot,
                    Status = _vein.Value,
                    DurationInCombatRounds = -1,
                    Caster = null
                });
            }
        }
        catch (Exception exception)
        {
            Plugin.ModLog.LogError($"Placing Lightning mana veins failed: {exception}");
        }
    }
}

/// <summary>
/// Fire Archmage: once a day, outside battle, flame burns away one trauma (preferred) or
/// scar completely, and the mage is Renewed. Traumas are healed through the game's own
/// injury bookkeeping; the scar that healing leaves, and its dislike, are then removed the
/// way the game removes scars.
/// </summary>
internal static class FireRenewal
{
    internal const string RenewedKey = "ArchmageProgression_FireRenewed";
    internal const string CooldownKey = "ArchmageProgression_FireRenewalCooldown";
    private static readonly List<DefId<InjuryDefinition>> InjuryScratch = new();
    private static readonly List<DefId<CharacterStatusConfig>> StatusScratch = new();

    internal static bool TryConfigure(CharacterStatusConfig definition)
    {
        var key = definition.Id.Key;
        if (key == RenewedKey)
        {
            var bonus = Plugin.FireRenewalConviction.Value;
            if (definition.NeedRateChanges is { } changes)
            {
                foreach (var change in changes)
                {
                    change.TargetBias = bonus / 100f;
                }
            }

            if (definition.Description?.Text is { } text)
            {
                definition.Description.Text = text.Replace("{Bonus}", SchoolRanks.Number(bonus));
            }

            return true;
        }

        if (key == CooldownKey)
        {
            definition.DurationInGameHours = Math.Max(1f, Plugin.FireRenewalIntervalHours.Value);
            return true;
        }

        return false;
    }

    internal static void TryRenew(Entity entity, CharacterStatusComponent statuses)
    {
        if (!Plugin.FireArchmageRenewal.Value || !RankPowers.HasRank(statuses, Skill.Pyromancy, archmageOnly: true) ||
            CharacterStatusUtils.HasStatusKey(CooldownKey, statuses) || BattleUtils.TryGetBattle(entity, out _) ||
            Simulation.Instance?.Configs?.InjuryCatalog is not { } catalog)
        {
            return;
        }

        string? burned = null;
        try
        {
            burned = BurnTrauma(entity, statuses, catalog) ?? BurnScar(entity, statuses, catalog);
        }
        catch (Exception exception)
        {
            Plugin.ModLog.LogError($"Fire renewal failed for {EntityUtils.GetDisplayName(entity)}: {exception}");
        }
        finally
        {
            InjuryScratch.Clear();
            StatusScratch.Clear();
        }

        if (burned is null)
        {
            return;
        }

        CharacterStatusUtils.AddStatusKey(CooldownKey, statuses);
        CharacterStatusUtils.AddStatusKey(RenewedKey, statuses);
        var name = EntityUtils.GetDisplayName(entity);
        Simulation.Instance.LogBook?.AddLogEntry(LogCategory.Notice, entity,
            $"{name}'s flame burned away {burned}. {name} is Renewed by Flame.");
        Plugin.ModLog.LogInfo($"Fire renewal burned away {burned} from {name}.");
    }

    private static string? BurnTrauma(Entity entity, CharacterStatusComponent statuses, InjuryCatalog catalog)
    {
        if (entity.TryGetComponent<InjuryOwnerComponent>(out var owner) && owner.CurrentInjuries is { Count: > 0 } injuries)
        {
            InjuryScratch.AddRange(injuries.Keys);
            foreach (var injury in InjuryScratch)
            {
                if (injury.GetDefinition() is not { } definition ||
                    !catalog.TraumaStatuses.Contains(definition.StatusToApply))
                {
                    continue;
                }

                var name = CharacterStatusUtils.GetDisplayName(definition.StatusToApply);
                InjuryUtils.HealInjury(entity, injury);
                RemoveLeftoverScars(entity, statuses, definition.StatusToApply);
                return $"the trauma {name}";
            }
        }

        // A trauma status without an injury record (older saves): remove it directly.
        StatusScratch.AddRange(statuses.Statuses.Keys);
        foreach (var key in StatusScratch)
        {
            if (catalog.TraumaStatuses.Contains(key))
            {
                var name = CharacterStatusUtils.GetDisplayName(key);
                CharacterStatusUtils.RemoveStatus(key, entity);
                RemoveLeftoverScars(entity, statuses, key);
                return $"the trauma {name}";
            }
        }

        return null;
    }

    /// <summary>Healing a trauma adds its scar on removal; burn that away too.</summary>
    private static void RemoveLeftoverScars(Entity entity, CharacterStatusComponent statuses, DefId<CharacterStatusConfig> trauma)
    {
        if (trauma.GetDefinition()?.AddStatusOnRemove is not { } scars)
        {
            return;
        }

        foreach (var scar in scars)
        {
            if (CharacterStatusUtils.HasStatus(scar, statuses))
            {
                CharacterStatusUtils.RemoveStatus(scar, entity);
            }
        }
    }

    private static string? BurnScar(Entity entity, CharacterStatusComponent statuses, InjuryCatalog catalog)
    {
        StatusScratch.Clear();
        StatusScratch.AddRange(statuses.Statuses.Keys);
        foreach (var key in StatusScratch)
        {
            if (catalog.ScarToTraumaStatusLookup.ContainsKey(key))
            {
                var name = CharacterStatusUtils.GetDisplayName(key);
                CharacterStatusUtils.RemoveStatus(key, entity);
                return $"the scar {name}";
            }
        }

        return null;
    }
}

/// <summary>
/// Dark Archmage: twice a day (at midnight and noon), each Archmage of Necromancy gathers a
/// bounty of a random dark reagent, delivered at the school entrance for haulers to store.
/// The first tick after a load only records the current half-day, so loading never pays out.
/// </summary>
[HarmonyPatch(typeof(UpdateCalendarSystem), nameof(UpdateCalendarSystem.Update))]
internal static class DarkReagentBountyPatch
{
    private static int _lastSlot = -1;
    private static Simulation? _simulation;
    private static readonly MethodInfo? CreateNearEntrance = Array.Find(
        typeof(EntityPlacementUtils).GetMethods(BindingFlags.Public | BindingFlags.Static),
        method => method.Name == "CreateEntitiesNearEntrance" && method.GetParameters().Length == 5);

    private static bool Prepare() =>
        Plugin.DarkArchmageBountyAmount.Value > 0 && Reagents().Length > 0;

    private static string[] Reagents() =>
        Array.FindAll(
            Array.ConvertAll(Plugin.DarkArchmageBountyReagents.Value.Split(','), part => part.Trim()),
            part => part.Length > 0);

    [HarmonyPostfix]
    private static void PayBounty(Simulation __0, TimeUtils.SimTime __3)
    {
        if (__0 is null)
        {
            return;
        }

        var slot = __3.Day * 2 + (TimeUtils.HourFromSimTime(__3) >= 12 ? 1 : 0);
        if (!ReferenceEquals(__0, _simulation) || _lastSlot < 0 || slot < _lastSlot)
        {
            _simulation = __0;
            _lastSlot = slot;
            return;
        }

        if (slot == _lastSlot)
        {
            return;
        }

        _lastSlot = slot;
        if (CreateNearEntrance is null)
        {
            return;
        }

        try
        {
            var reagents = Reagents();
            foreach (var archmage in SchoolPowers.DarkArchmages)
            {
                if (archmage.IsDestroyed)
                {
                    continue;
                }

                var key = reagents[__0.Random.Next(reagents.Length)];
                if (!ConfigData.Instance.TryGetArchetypeFromStringKey(key, out var archetype))
                {
                    Plugin.ModLog.LogWarning($"Reagent bounty: unknown reagent '{key}'.");
                    continue;
                }

                var amount = Plugin.DarkArchmageBountyAmount.Value;
                CreateNearEntrance.Invoke(null, new object?[] { archetype.EntityKey, amount, null, null, null });
                var name = EntityUtils.GetDisplayName(archmage);
                __0.LogBook?.AddLogEntry(LogCategory.Notice, archmage,
                    $"{name} gathered a reagent bounty: {amount} <slink={key}>, delivered to the school entrance.");
                Plugin.ModLog.LogInfo($"Reagent bounty: {name} gathered {amount} {key}.");
            }
        }
        catch (Exception exception)
        {
            Plugin.ModLog.LogError($"Reagent bounty failed: {exception}");
        }
    }
}

/// <summary>
/// Earth Archmage: the school may build Nexus Gateways. The buildable archetype is a copy of
/// the Underschool gateway injected from the plugin's Content folder; the build menu hides
/// it, through the game's own research-lock check, while the school has no Earth Archmage.
/// </summary>
internal static class NexusGateway
{
    internal const string Key = "ArchmageProgression_NexusGateway";
}

[HarmonyPatch(typeof(ConfigData), nameof(ConfigData.Load))]
internal static class NexusGatewayCatalogPatch
{
    [ThreadStatic] private static bool _loadingMod;

    private static bool Prepare() => Plugin.EarthArchmageNexusGateways.Value;

    [HarmonyPostfix]
    private static void Inject(ConfigData __instance, YamlParserType __1)
    {
        // Only real game catalogs (they hold the Broom Rack); never temporary mod catalogs.
        if (_loadingMod || !__instance.TryGetArchetypeFromStringKey("BroomStation", out _) ||
            __instance.TryGetArchetypeFromStringKey(NexusGateway.Key, out _))
        {
            return;
        }

        var root = SchoolRanks.ContentRoot;
        if (!Directory.Exists(Path.Combine(root, "Entities")))
        {
            Plugin.ModLog.LogWarning($"Nexus Gateway definition not found under {root}; Earth Archmages cannot build gateways.");
            return;
        }

        _loadingMod = true;
        try
        {
            var modArchetypes = new ConfigData();
            modArchetypes.Load(root, __1);
            foreach (var definition in modArchetypes.AllDefinitions())
            {
                if (definition?.EntityKey.Key == NexusGateway.Key)
                {
                    __instance.Add(definition);
                    Plugin.ModLog.LogInfo("Injected the buildable Nexus Gateway archetype.");
                }
            }
        }
        catch (Exception exception)
        {
            Plugin.ModLog.LogError($"Failed to inject the Nexus Gateway archetype: {exception}");
        }
        finally
        {
            _loadingMod = false;
        }
    }
}

[HarmonyPatch(typeof(BuildableUtils), nameof(BuildableUtils.GetBuildableArchetypes))]
internal static class NexusGatewayBuildablePatch
{
    private static bool Prepare() => Plugin.EarthArchmageNexusGateways.Value;

    [HarmonyPostfix]
    private static void Include(ref List<Archetype> __0)
    {
        if (__0 is not null && ConfigData.Instance is { } catalog &&
            catalog.TryGetArchetypeFromStringKey(NexusGateway.Key, out var gateway) && !__0.Contains(gateway))
        {
            __0.Add(gateway);
        }
    }
}

[HarmonyPatch(typeof(ResearchUtils), nameof(ResearchUtils.IsKeyLockedByResearch), new[] { typeof(DefId<Archetype>) })]
internal static class NexusGatewayLockPatch
{
    private static bool Prepare() => Plugin.EarthArchmageNexusGateways.Value;

    [HarmonyPostfix]
    private static void LockWithoutArchmage(DefId<Archetype> __0, ref bool __result)
    {
        if (!__result && __0.Key == NexusGateway.Key && !SchoolPowers.EarthArchmagePresent)
        {
            __result = true;
        }
    }
}

/// <summary>
/// Council: a boss rematch pays what the first victory paid, relic included. The rematch
/// effigy normally rolls its own small placement list; while the council power is in force,
/// the boss room's own reward placements (those with drops) are rolled instead.
/// </summary>
[HarmonyPatch(typeof(DungeonRunUtils), nameof(DungeonRunUtils.ExploreCompleted))]
internal static class CouncilRematchRunPatch
{
    [ThreadStatic] internal static DungeonRun? Current;

    private static bool Prepare() => Plugin.CouncilRematchArchmages.Value > 0;

    [HarmonyPrefix]
    private static void Remember(DungeonRun __0) => Current = __0;

    [HarmonyFinalizer]
    private static Exception? Forget(Exception? __exception)
    {
        Current = null;
        return __exception;
    }
}

[HarmonyPatch(typeof(UndergroundUtils), nameof(UndergroundUtils.GeneratePlacementsNearLocation))]
internal static class CouncilRematchRewardsPatch
{
    private static bool Prepare() => Plugin.CouncilRematchArchmages.Value > 0;

    [HarmonyPrefix]
    private static void FirstVictoryRewards(ref List<DungeonRoomPlacement> __1)
    {
        if (CouncilRematchRunPatch.Current is not { } run || !SchoolPowers.Unlocked(Plugin.CouncilRematchArchmages.Value))
        {
            return;
        }

        try
        {
            if (run.RunStartEntities is not { Count: > 0 } starts || starts[0] is not { } effigy ||
                !effigy.TryGetComponent<TransformComponent>(out var transform) ||
                RoomUtils.FindRoom(transform) is not { } room ||
                !RoomUtils.TryGetRoomComponent(room, out var roomComponent) ||
                roomComponent.DungeonRoomDefId.GetDefinition() is not { IsBossRoom: true } boss ||
                boss.Placements is null)
            {
                Plugin.ModLog.LogInfo("Archmage Council: rematch rewards unchanged (no boss room found).");
                return;
            }

            var rewards = boss.Placements.FindAll(placement => placement.Drops != null);
            if (rewards.Count > 0)
            {
                __1 = rewards;
                Plugin.ModLog.LogInfo($"Archmage Council: boss rematch pays the first-victory rewards of {boss.Id.Key}.");
            }
        }
        catch (Exception exception)
        {
            Plugin.ModLog.LogError($"Council rematch rewards failed: {exception}");
        }
    }
}

/// <summary>
/// Council: scouting a faction reveals every eligible side quest instead of at most two.
/// The cap is an inlined constant, so the two cap comparisons are rewritten to call
/// SideQuestCap; anything unexpected leaves the method untouched.
/// </summary>
[HarmonyPatch(typeof(TravelQuestUtils), "HandleScoutingCompleted")]
internal static class CouncilScoutingRevealPatch
{
    private static bool Prepare() => Plugin.CouncilScoutingArchmages.Value > 0;

    internal static int SideQuestCap() =>
        SchoolPowers.Unlocked(Plugin.CouncilScoutingArchmages.Value) ? int.MaxValue : 2;

    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> RaiseCap(IEnumerable<CodeInstruction> instructions)
    {
        var code = new List<CodeInstruction>(instructions);
        var matches = new List<int>();
        for (var i = 0; i < code.Count - 1; i++)
        {
            if (code[i].opcode == OpCodes.Ldc_I4_2 &&
                (code[i + 1].opcode == OpCodes.Bge || code[i + 1].opcode == OpCodes.Bge_S))
            {
                matches.Add(i);
            }
        }

        if (matches.Count != 2)
        {
            Plugin.ModLog.LogWarning(
                $"Scouting reveal: expected 2 side-quest caps, found {matches.Count}; leaving scouting unchanged.");
            return code;
        }

        var cap = AccessTools.Method(typeof(CouncilScoutingRevealPatch), nameof(SideQuestCap));
        foreach (var index in matches)
        {
            // Rewrite in place so any branch labels on the constant stay attached.
            code[index].opcode = OpCodes.Call;
            code[index].operand = cap;
        }

        return code;
    }
}

[HarmonyPatch(typeof(TravelQuestUtils), nameof(TravelQuestUtils.NumQuestsAvailableForScouting))]
internal static class CouncilScoutingCountPatch
{
    private static readonly Func<TravelQuest, IEnumerable<QuestDefinition>>? MainQuests = Delegate("GetPotentialMainQuests");
    private static readonly Func<TravelQuest, IEnumerable<QuestDefinition>>? SideQuests = Delegate("GetPotentialSideQuests");

    private static Func<TravelQuest, IEnumerable<QuestDefinition>>? Delegate(string name) =>
        AccessTools.Method(typeof(TravelQuestUtils), name) is { } method
            ? AccessTools.MethodDelegate<Func<TravelQuest, IEnumerable<QuestDefinition>>>(method)
            : null;

    private static bool Prepare() =>
        Plugin.CouncilScoutingArchmages.Value > 0 && MainQuests is not null && SideQuests is not null;

    [HarmonyPostfix]
    private static void CountAll(TravelQuest __0, ref int __result)
    {
        if (__0 is null || !SchoolPowers.Unlocked(Plugin.CouncilScoutingArchmages.Value))
        {
            return;
        }

        try
        {
            __result = MainQuests!(__0).Count() + SideQuests!(__0).Count();
        }
        catch (Exception exception)
        {
            Plugin.ModLog.LogError($"Scouting count failed: {exception}");
        }
    }
}

/// <summary>
/// Council: quests get shorter. Each Archmage rank takes a tenth off the travel still to
/// run; at ten the party lands home on the next tick. Remaining time is cut when a quest
/// starts and rescaled whenever the rank count changes, so every quest UI stays accurate.
/// </summary>
internal static class SwiftTravel
{
    private static Simulation? _simulation;
    private static int _appliedCount;

    internal static float Factor(int archmages) =>
        Math.Max(0f, 1f - Math.Max(0f, Plugin.CouncilTravelCutPerArchmage.Value) * archmages);

    internal static bool Enabled => Plugin.CouncilEnabled.Value && Plugin.CouncilTravelCutPerArchmage.Value > 0f;

    /// <summary>The count already applied to this save's quests, or 0 before the first sweep.</summary>
    internal static int AppliedCount =>
        ReferenceEquals(_simulation, Simulation.Instance) ? _appliedCount : 0;

    /// <summary>Called by the sweep with the current count; rescales quests in progress.</summary>
    internal static void Update(Simulation simulation, int archmages)
    {
        if (!ReferenceEquals(_simulation, simulation))
        {
            // First sweep for this save: the saved TimeRemaining values already carry the cut.
            _simulation = simulation;
            _appliedCount = archmages;
            return;
        }

        if (archmages == _appliedCount || !Enabled)
        {
            _appliedCount = archmages;
            return;
        }

        var before = Factor(_appliedCount);
        var after = Factor(archmages);
        _appliedCount = archmages;
        foreach (var giver in simulation.ComponentManager.AllEnumerator<QuestGiverComponent>())
        {
            if (giver?.ActiveTravelQuests is not { } quests)
            {
                continue;
            }

            foreach (var quest in quests)
            {
                if (quest is null || quest.State != TravelQuestState.InProgress)
                {
                    continue;
                }

                if (after <= 0f)
                {
                    quest.TimeRemaining = 0f;
                }
                else if (before > 0f)
                {
                    quest.TimeRemaining *= after / before;
                }
            }
        }

        Plugin.ModLog.LogInfo($"Archmage Council: quest travel now runs at {SchoolRanks.Number(after * 100f)}% of normal.");
    }
}

[HarmonyPatch(typeof(TravelQuestUtils), nameof(TravelQuestUtils.StartQuest))]
internal static class CouncilSwiftTravelPatch
{
    private static bool Prepare() => Plugin.CouncilTravelCutPerArchmage.Value > 0f;

    [HarmonyPostfix]
    private static void Shorten(TravelQuest __0)
    {
        if (!SwiftTravel.Enabled || __0 is null || __0.State != TravelQuestState.InProgress)
        {
            return;
        }

        __0.TimeRemaining *= SwiftTravel.Factor(SwiftTravel.AppliedCount);
    }
}
