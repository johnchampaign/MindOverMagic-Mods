using System;
using System.Collections.Generic;
using System.Globalization;

namespace ArchmageProgression;

/// <summary>
/// One player-tunable value. Declared once here and surfaced by whichever host loads the
/// mod: the BepInEx build binds it into its .cfg file under <see cref="Section"/> and
/// <see cref="Key"/>; the official-mod build shows it on the game's settings screen under
/// the shorter <see cref="Label"/> and stores it under "Section/Key".
/// </summary>
internal abstract class Setting
{
    protected Setting(string section, string key, string label, string description, double? min, double? max)
    {
        Section = section;
        Key = key;
        Label = label;
        Description = description;
        Min = min;
        Max = max;
    }

    internal string Section { get; }
    internal string Key { get; }
    internal string Label { get; }
    internal string Description { get; }
    internal double? Min { get; }
    internal double? Max { get; }
    internal string StoreKey => $"{Section}/{Key}";

    /// <summary>The current value as invariant text; setting it ignores unparsable text.</summary>
    internal abstract string StoredValue { get; set; }

    internal abstract string DefaultStoredValue { get; }
}

internal sealed class Setting<T> : Setting
{
    internal Setting(string section, string key, T defaultValue, string label, string description,
        double? min = null, double? max = null)
        : base(section, key, label, description, min, max)
    {
        DefaultValue = defaultValue;
        Value = defaultValue;
    }

    internal T DefaultValue { get; }
    internal T Value { get; set; }

    internal override string DefaultStoredValue =>
        Convert.ToString(DefaultValue, CultureInfo.InvariantCulture) ?? string.Empty;

    internal override string StoredValue
    {
        get => Convert.ToString(Value, CultureInfo.InvariantCulture) ?? string.Empty;
        set
        {
            try
            {
                Value = (T)Convert.ChangeType(value, typeof(T), CultureInfo.InvariantCulture);
            }
            catch (Exception exception) when (exception is FormatException or InvalidCastException or OverflowException)
            {
                // Keep the current value; a hand-edited typo should not take the mod down.
            }
        }
    }
}

/// <summary>Logging that routes to whichever host loaded the mod.</summary>
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

/// <summary>Host-independent mod identity, settings and logging.</summary>
internal static class Plugin
{
    public const string PluginGuid = "ca.johnc.mindovermagic.archmageprogression";
    public const string PluginName = "Archmage Progression";
    public const string PluginVersion = "0.3.0";

#if OFFICIAL_MOD
    public const string ModId = "johnc.archmageprogression";

    /// <summary>The game namespaces a mod's definitions with its mod id.</summary>
    internal const string KeyPrefix = ModId + ".";
#else
    internal const string KeyPrefix = "";
#endif

    internal static ModLogger ModLog { get; set; } = new(_ => { }, _ => { }, _ => { });

    /// <summary>Every setting, in the order they are declared below.</summary>
    internal static readonly List<Setting> All = new();

    private static Setting<T> Define<T>(string section, string key, T defaultValue, string label,
        string description, double? min = null, double? max = null)
    {
        var setting = new Setting<T>(section, key, defaultValue, label, description, min, max);
        All.Add(setting);
        return setting;
    }

    private const string Skills = "Per-skill bonuses";
    private const string Breakdown = "Stat breakdown";
    private const string Statuses = "Rank statuses";
    private const string Powers = "Rank powers";
    private const string Council = "Archmage Council";

    internal static readonly Setting<float> ManaPerPyromancy = Define(
        Skills, "Mana per Pyromancy", 5f, "Mana per Fire level",
        "Maximum Mana added for each Pyromancy skill level. Restart required.", 0, 20);
    internal static readonly Setting<float> HitPointsPerGeomancy = Define(
        Skills, "HP per Geomancy", 5f, "HP per Earth level",
        "Maximum HP added for each Geomancy skill level. Restart required.", 0, 20);
    internal static readonly Setting<float> DamagePerDivination = Define(
        Skills, "Damage bonus per Divination", 2f, "Damage per Lightning",
        "Combat Damage Bonus added for each Divination skill level. Restart required.", 0, 10);
    internal static readonly Setting<float> DodgePerManipulation = Define(
        Skills, "Dodge per Manipulation", 2f, "Dodge per Air level",
        "Combat Dodge Chance added for each Manipulation skill level. Restart required.", 0, 10);
    internal static readonly Setting<float> RegenPerHydrokinesis = Define(
        Skills, "Combat regeneration per Hydrokinesis", 1f, "Regen per Water level",
        "Combat regeneration added for each Hydrokinesis skill level. Restart required.", 0, 5);
    internal static readonly Setting<float> NeedDecayReductionPerNecromancy = Define(
        Skills, "Need decay reduction per Necromancy", 0.02f, "Slower decay per Dark",
        "Fractional reduction to need decay for each Necromancy skill level (0.02 = 2%). Restart required.", 0, 0.1);
    internal static readonly Setting<float> ConvictionPerViturgy = Define(
        Skills, "Conviction per Viturgy", 2f, "Conviction per Nature",
        "Conviction target added for each Viturgy (Nature) skill level. It appears as " +
        "'Viturgy Attunement' in the Status & Conviction panel. Set to 0 to disable. Restart required.", 0, 10);

    internal static readonly Setting<bool> StatBreakdownEnabled = Define(
        Breakdown, "Enabled", true, "Show stat breakdown",
        "List each school's contribution in the game's stat hover text: the HP and Mana bars " +
        "on the selected-mage panel, and the stat icons on the Mage Sheet. Restart required.");

    internal static readonly Setting<bool> RankStatusesEnabled = Define(
        Statuses, "Enabled", true, "Show rank badges",
        "Show an Adept or Archmage status on each mage for every magic school they have " +
        "trained far enough. The statuses are informational badges; the per-skill bonuses " +
        "above apply regardless. Restart required.");
    internal static readonly Setting<int> AdeptSkillLevel = Define(
        Statuses, "Adept skill level", 5, "Adept skill level",
        "Skill level at which a mage earns the Adept rank for a school. Set to 0 to " +
        "disable the Adept rank. Restart required.", 0, 8);
    internal static readonly Setting<int> ArchmageSkillLevel = Define(
        Statuses, "Archmage skill level", 8, "Archmage skill level",
        "Skill level at which a mage earns the Archmage rank for a school. The base game " +
        "caps skills at 8. Set to 0 to disable the Archmage rank. Restart required.", 0, 8);

    internal static readonly Setting<float> AirWalkSpeedMultiplier = Define(
        Powers, "Air Adept walk speed multiplier", 2f, "Air Adept walk speed",
        "Walking speed multiplier for Adepts and Archmages of Manipulation (Air). " +
        "1 disables. Restart required.", 1, 4);
    internal static readonly Setting<bool> AirArchmageGhostMovement = Define(
        Powers, "Air Archmage flight and phasing", true, "Air Archmage flight",
        "Archmages of Manipulation fly and pass through walls and floors, like the school's " +
        "founder. Restart required.");
    internal static readonly Setting<float> FireCookingSpeedBonus = Define(
        Powers, "Fire Adept cooking speed bonus", 1f, "Fire Adept cooking",
        "Extra cooking speed for Adepts and Archmages of Pyromancy (Fire): 1 = twice as fast. " +
        "0 disables. Restart required.", 0, 4);
    internal static readonly Setting<bool> FireBattleCounterattack = Define(
        Powers, "Fire Adept battle counterattack", true, "Fire counterattack",
        "Adepts and Archmages of Pyromancy start every battle with Counterattack for two " +
        "rounds. Restart required.");
    internal static readonly Setting<bool> DarkArchmageNeedsSated = Define(
        Powers, "Dark Archmage needs stay sated", true, "Dark Archmage sated",
        "Archmages of Necromancy (Dark) suffer no ordinary need decay. Conviction and Mana " +
        "are unaffected. Restart required.");
    internal static readonly Setting<float> NatureArchmageConvictionToOthers = Define(
        Powers, "Nature Archmage Conviction to others", 5f, "Nature Archmage aura",
        "Conviction target every other mage gains from each Archmage of Viturgy (Nature). " +
        "0 disables. Restart required.", 0, 20);
    internal static readonly Setting<float> EarthAdeptBattleArmour = Define(
        Powers, "Earth Adept battle armour", 0.5f, "Earth Adept armour",
        "Armour granted to Adepts and Archmages of Geomancy (Earth) at the start of every " +
        "battle, as a fraction of Max HP (0.5 = 50%). 0 disables. Restart required.", 0, 1);
    internal static readonly Setting<bool> EarthArchmageImmunity = Define(
        Powers, "Earth Archmage immunity", true, "Earth Archmage immunity",
        "Archmages of Geomancy cannot be given harmful combat effects (the debuffs the " +
        "Sanctified terrain cleanses, such as Stunned, Blinded, Burns and Fear). Restart required.");
    internal static readonly Setting<bool> WaterAdeptCleanse = Define(
        Powers, "Water Adept cleanse", true, "Water Adept cleanse",
        "Adepts and Archmages of Hydrokinesis (Water) shed harmful combat effects at the " +
        "start of every battle round. Restart required.");
    internal static readonly Setting<bool> LightningArchmageFreeSpells = Define(
        Powers, "Lightning Archmage free spells", true, "Lightning free spells",
        "Archmages of Divination (Lightning) cast every spell, in battle or at school, " +
        "without spending mana. Restart required.");
    internal static readonly Setting<float> DarkAdeptLifesteal = Define(
        Powers, "Dark Adept lifesteal", 0.1f, "Dark Adept lifesteal",
        "Share of the damage their spells deal that Adepts and Archmages of Necromancy " +
        "(Dark) regain as HP (0.1 = 10%). 0 disables. Restart required.", 0, 0.5);
    internal static readonly Setting<float> DarkAdeptHealOnEnemyDeath = Define(
        Powers, "Dark Adept heal on enemy death", 25f, "Dark heal on foe death",
        "HP Adepts and Archmages of Necromancy regain whenever a foe falls in battle. " +
        "0 disables. Restart required.", 0, 100);
    internal static readonly Setting<float> NatureAdeptLuxury = Define(
        Powers, "Nature Adept room Luxury", 2f, "Nature Adept luxury",
        "Luxury added to every room while the school has at least one Adept or Archmage " +
        "of Viturgy (Nature). Does not stack. 0 disables. Restart required.", 0, 10);
    internal static readonly Setting<bool> LightningAdeptManaVein = Define(
        Powers, "Lightning Adept mana vein", true, "Lightning mana vein",
        "Adepts and Archmages of Divination (Lightning) start every battle standing on a " +
        "mana vein, which halves spell costs while they stay on it. Restart required.");
    internal static readonly Setting<bool> FireArchmageRenewal = Define(
        Powers, "Fire Archmage renewal", true, "Fire Archmage renewal",
        "Archmages of Pyromancy (Fire) periodically burn away one of their own traumas, or " +
        "failing that a scar, and are Renewed by Flame. Restart required.");
    internal static readonly Setting<float> FireRenewalIntervalHours = Define(
        Powers, "Fire Archmage renewal interval (game hours)", 24f, "Renewal every (hours)",
        "Game hours between two renewals for the same Archmage. Restart required.", 1, 96);
    internal static readonly Setting<float> FireRenewalConviction = Define(
        Powers, "Fire Archmage renewal Conviction", 10f, "Renewal Conviction",
        "Conviction target granted by Renewed by Flame for a day. Restart required.", 0, 30);
    internal static readonly Setting<int> DarkArchmageBountyAmount = Define(
        Powers, "Dark Archmage reagent bounty amount", 5, "Dark reagent bounty",
        "Reagents each Archmage of Necromancy (Dark) gathers at midnight and at noon, " +
        "delivered to the school entrance. 0 disables. Restart required.", 0, 20);
    internal static readonly Setting<string> DarkArchmageBountyReagents = Define(
        Powers, "Dark Archmage reagent bounty reagents",
        "Voidcap,MandrakeRoot,Bile,Brains,Viscera,ReapersCap", "Bounty reagents",
        "Comma-separated item keys; each bounty picks one at random. Restart required.");
    internal static readonly Setting<bool> EarthArchmageNexusGateways = Define(
        Powers, "Earth Archmage Nexus Gateways", true, "Nexus Gateways",
        "While the school has an Archmage of Geomancy (Earth), Nexus Gateways can be built. " +
        "Gateways teleport to each other. Restart required.");
    internal static readonly Setting<bool> WaterArchmageWatersOfReturn = Define(
        Powers, "Water Archmage Waters of Return", true, "Waters of Return",
        "While the school has an Archmage of Hydrokinesis (Water), the Waters of Return " +
        "fountain can be built. Its ritual raises the mage buried in a grave in the same " +
        "room. Restart required.");

    internal static readonly Setting<bool> CouncilEnabled = Define(
        Council, "Enabled", true, "Archmage Council",
        "School-wide powers based on how many Archmage ranks the school holds in total. " +
        "The school's founder shows an 'Archmage Council' badge listing the powers in " +
        "force. Restart required.");
    internal static readonly Setting<int> CouncilResolveArchmages = Define(
        Council, "Resolve: Archmage ranks needed", 3, "Resolve: ranks needed",
        "Archmage ranks the school needs before every mage gains the Resolve bonus. " +
        "0 disables. Restart required.", 0, 30);
    internal static readonly Setting<float> CouncilResolveConviction = Define(
        Council, "Resolve: Conviction bonus", 10f, "Resolve: Conviction",
        "Conviction target every mage gains from Resolve. Restart required.", 0, 30);
    internal static readonly Setting<int> CouncilScholarshipArchmages = Define(
        Council, "Scholarship: Archmage ranks needed", 5, "Scholarship: ranks",
        "Archmage ranks the school needs before teaching and learning speed up. " +
        "0 disables. Restart required.", 0, 30);
    internal static readonly Setting<float> CouncilScholarshipSpeedBonus = Define(
        Council, "Scholarship: speed bonus", 1f, "Scholarship: bonus",
        "Extra teaching and learning speed from Scholarship: 1 = twice as fast. Restart required.", 0, 4);
    internal static readonly Setting<int> CouncilRefiningArchmages = Define(
        Council, "Refining: Archmage ranks needed", 2, "Refining: ranks",
        "Archmage ranks the school needs before refineries hand back more. 0 disables. Restart required.", 0, 30);
    internal static readonly Setting<float> CouncilRefiningMultiplier = Define(
        Council, "Refining: output multiplier", 2f, "Refining: multiplier",
        "Output multiplier for the Earth, Air, Fire and Dark refineries. Restart required.", 1, 4);
    internal static readonly Setting<int> CouncilMendingArchmages = Define(
        Council, "Mending: Archmage ranks needed", 6, "Mending: ranks",
        "Archmage ranks the school needs before wounds close faster. 0 disables. Restart required.", 0, 30);
    internal static readonly Setting<float> CouncilMendingMultiplier = Define(
        Council, "Mending: healing speed multiplier", 3f, "Mending: multiplier",
        "How much faster wounds (trauma injuries) close. Restart required.", 1, 6);
    internal static readonly Setting<int> CouncilSteadyMindsArchmages = Define(
        Council, "Steady Minds: Archmage ranks needed", 7, "Steady Minds: ranks",
        "Archmage ranks the school needs before mental breaks stop leaving mages At " +
        "Death's Door. 0 disables. Restart required.", 0, 30);
    internal static readonly Setting<int> CouncilRematchArchmages = Define(
        Council, "Rematch: Archmage ranks needed", 1, "Rematch: ranks",
        "Archmage ranks the school needs before boss rematches pay the first-victory rewards, " +
        "relic included. 0 disables. Restart required.", 0, 30);
    internal static readonly Setting<int> CouncilScoutingArchmages = Define(
        Council, "Open Ledgers: Archmage ranks needed", 4, "Open Ledgers: ranks",
        "Archmage ranks the school needs before scouting a faction reveals every side quest " +
        "instead of two. 0 disables. Restart required.", 0, 30);
    internal static readonly Setting<float> CouncilTravelCutPerArchmage = Define(
        Council, "Swift Travel: cut per Archmage rank", 0.1f, "Swift Travel per rank",
        "Share of remaining quest travel removed per Archmage rank (0.1 = a tenth each; ten " +
        "ranks bring parties home at once). 0 disables. Restart required.", 0, 0.5);
}
