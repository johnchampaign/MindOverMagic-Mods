using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Model;
using Model.Modding;

namespace ArchmageProgression;

/// <summary>
/// The host for the game's own mod support. Statuses and buildings arrive through the
/// mod's Defs folder, settings through the Mods screen, and the periodic sweep through a
/// registered simulation process; everything else is the same code the BepInEx build runs.
/// </summary>
public sealed class OfficialEntry : IMod, IModSettingsUI
{
    private StoredSettings _stored = new();

    public void OnInitialize(IModContext context)
    {
        Plugin.ModLog = new ModLogger(context.Logger.Log, context.Logger.LogWarning, context.Logger.LogError);

        // Settings are read before any patch is applied: several patches decide in
        // Prepare() whether to apply at all.
        _stored = context.Settings.Get<StoredSettings>();
        foreach (var setting in Plugin.All)
        {
            if (_stored.Values.TryGetValue(setting.StoreKey, out var text))
            {
                setting.StoredValue = text;
            }
        }

        var changed = Plugin.All
            .Where(setting => setting.StoredValue != setting.DefaultStoredValue)
            .Select(setting => $"{setting.StoreKey} = {setting.StoredValue}")
            .ToList();
        Plugin.ModLog.LogInfo(changed.Count == 0
            ? "All settings are at their defaults."
            : $"Settings changed from their defaults: {string.Join("; ", changed)}.");
    }

    public void OnLoad(ConfigBundle configBundle, IModContext context)
    {
        var tuned = 0;
        foreach (var definition in configBundle.CharacterStatusCatalog.AllDefinitions())
        {
            if (definition?.Id.Key is { } key && key.StartsWith(Plugin.KeyPrefix, StringComparison.Ordinal))
            {
                StatusDescriptions.Fill(definition);
                tuned++;
            }
        }

        Plugin.ModLog.LogInfo($"Tuned {tuned} Archmage Progression statuses to this install's settings.");
        AddLegacyAliases(configBundle.CharacterStatusCatalog);

        var harmony = (Harmony)context.HarmonyInstance;
        try
        {
            harmony.PatchAll(typeof(OfficialEntry).Assembly);
        }
        catch (Exception exception)
        {
            // PatchAll may have applied earlier patches before a later invalid target
            // fails. Undo them so a game update cannot leave a partial mod active.
            harmony.UnpatchAll(harmony.Id);
            Plugin.ModLog.LogError($"{Plugin.PluginName} was safely disabled during initialization: {exception}");
            return;
        }

        Plugin.ModLog.LogInfo($"{Plugin.PluginName} {Plugin.PluginVersion} loaded.");
    }

    /// <summary>
    /// Register each status a second time under the un-namespaced key the BepInEx build
    /// used, so saves made with that build still resolve their statuses. The copies share
    /// everything but their id; LegacyStatuses then swaps them out of the save.
    /// </summary>
    private static void AddLegacyAliases(CharacterStatusConfigCatalog catalog)
    {
        var clone = typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic);
        if (clone is null)
        {
            Plugin.ModLog.LogWarning("Could not create aliases for statuses saved by the BepInEx build.");
            return;
        }

        var added = 0;
        foreach (var definition in catalog.AllDefinitions().ToList())
        {
            if (definition?.Id.Key is not { } key || !key.StartsWith(Plugin.KeyPrefix, StringComparison.Ordinal))
            {
                continue;
            }

            var legacyKey = key.Substring(Plugin.KeyPrefix.Length);
            if (!legacyKey.StartsWith(LegacyStatuses.Prefix, StringComparison.Ordinal) ||
                catalog.TryGetDefinitionFromStringKey(legacyKey, out _))
            {
                continue;
            }

            var alias = (CharacterStatusConfig)clone.Invoke(definition, null);
            alias.Id = new DefId<CharacterStatusConfig>
            {
                Key = legacyKey,
                Uid = DefinitionCatalog<CharacterStatusConfig>.GenerateUid(legacyKey)
            };
            catalog.Add(alias);
            added++;
        }

        Plugin.ModLog.LogInfo($"Registered {added} aliases for statuses saved by the BepInEx build.");
    }

    public void OnSimulationStart(Simulation simulation)
    {
    }

    public void OnWorldReady(Simulation simulation, SimulationStartReason reason, IModContext context)
    {
        // Every game-second, the cadence of the game's own skill-status system that the
        // BepInEx build piggybacks on.
        context.RegisterSimulationProcess(simulation, "Archmage Progression sweep", new Sweep(), 1f, 0f);
    }

    public void OnSimulationEnd()
    {
    }

    public void OnUnload()
    {
    }

    public IReadOnlyList<ModSettingField> SettingsFields
    {
        get
        {
            var fields = new List<ModSettingField>();
            string? section = null;
            foreach (var setting in Plugin.All)
            {
                if (setting.Section != section)
                {
                    section = setting.Section;
                    fields.Add(ModSettingField.Header(section));
                }

                fields.Add(Field(setting));
            }

            return fields;
        }
    }

    public void OnSettingsChanged()
    {
    }

    /// <summary>
    /// One row per setting. Each setter also writes the stored copy, which the game saves
    /// once the player presses OK.
    /// </summary>
    private ModSettingField Field(Setting setting)
    {
        void Store() => _stored.Values[setting.StoreKey] = setting.StoredValue;

        return setting switch
        {
            Setting<bool> flag => ModSettingField.Bool(flag.Label,
                () => flag.Value, value => { flag.Value = value; Store(); }, flag.Description),
            Setting<int> whole => ModSettingField.Int(whole.Label,
                () => whole.Value, value => { whole.Value = value; Store(); },
                (int?)whole.Min, (int?)whole.Max, whole.Description),
            Setting<float> number => ModSettingField.Float(number.Label,
                () => number.Value, value => { number.Value = value; Store(); },
                (float?)number.Min, (float?)number.Max, number.Description),
            Setting<string> text => ModSettingField.Text(text.Label,
                () => text.Value, value => { text.Value = value; Store(); }, 0, text.Description),
            _ => throw new InvalidOperationException($"Unsupported setting type for {setting.StoreKey}.")
        };
    }

    private sealed class Sweep : ISimProcessor
    {
        public IEnumerator Update(Simulation sim, float dt, TimeUtils.SimTime lastTime,
            TimeUtils.SimTime currentTime, int iterationIndex, int iterationCount)
        {
            RankStatusSweepPatch.RefreshAllRanks(sim);
            yield break;
        }
    }
}

/// <summary>Persisted settings: "Section/Key" to invariant text, so the file reads plainly.</summary>
public sealed class StoredSettings
{
    public Dictionary<string, string> Values { get; set; } = new();
}
