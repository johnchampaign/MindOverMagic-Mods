using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SacrificialAltar;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public sealed class Plugin : BaseUnityPlugin
{
    public const string PluginGuid = "ca.johnc.mindovermagic.sacrificialaltar";
    public const string PluginName = "Sacrificial Altar";
    public const string PluginVersion = "1.3.5";
    internal static ManualLogSource ModLog { get; private set; } = null!;

    private void Awake()
    {
        ModLog = Logger;
        var harmony = new Harmony(PluginGuid);
        try
        {
            harmony.PatchAll();
        }
        catch (Exception exception)
        {
            // PatchAll can leave classes patched before a later invalid target
            // throws. Roll everything back so the base game can still start.
            harmony.UnpatchSelf();
            Logger.LogError(
                $"{PluginName} could not initialize and was safely disabled: {exception}");
            enabled = false;
            return;
        }
        Logger.LogInfo($"{PluginName} {PluginVersion} loaded.");
    }

}

internal static class ModContent
{
    internal static string Root => Path.Combine(Paths.PluginPath, "SacrificialAltar", "Content");
}

[HarmonyPatch(typeof(PrefabResources), nameof(PrefabResources.TryGetPrefabByPath))]
internal static class AltarPrefabPatch
{
    private const string CustomPrefabKey = "Mods/SacrificialAltar/Altar";
    private const string ShopPrefabKey = "Entities/" + CustomPrefabKey;
    private const string MaterialSourceKey = "Furniture/Crafting/Mortem/SoulAltar_4x4x3";
    private const string NativeSelectionPrefabKey =
        "Entities/Furniture/Rituals/CeremonyHall/CeremonyStation_3x3x1";
    private static GameObject? _prefab;

    [HarmonyPrefix]
    private static bool SupplyCustomPrefab(
        PrefabResources __instance,
        string __0,
        ref GameObject __1,
        ref bool __result)
    {
        if (__0 != CustomPrefabKey && __0 != ShopPrefabKey)
        {
            return true;
        }

        if (_prefab == null)
        {
            EnsureBuilt(__instance);
        }

        __1 = _prefab!;
        __result = true;
        return false;
    }

    internal static void EnsureBuilt(PrefabResources resources)
    {
        if (_prefab == null)
        {
            _prefab = BuildPrefab(resources);
        }
    }

    private static GameObject BuildPrefab(PrefabResources resources)
    {
        Material? sourceMaterial = null;
        if (resources.TryGetPrefabByPath(MaterialSourceKey, out var sourcePrefab))
        {
            sourceMaterial = sourcePrefab.GetComponentInChildren<Renderer>(true)?.sharedMaterial;
        }

        var stone = MakeMaterial(sourceMaterial, new Color(0.10f, 0.085f, 0.14f));
        var stoneEdge = MakeMaterial(sourceMaterial, new Color(0.18f, 0.16f, 0.23f));
        var wood = MakeMaterial(sourceMaterial, new Color(0.12f, 0.055f, 0.04f));
        var iron = MakeMaterial(sourceMaterial, new Color(0.12f, 0.11f, 0.13f));
        var crimson = MakeMaterial(sourceMaterial, new Color(0.34f, 0.015f, 0.025f));
        var violet = MakeMaterial(sourceMaterial, new Color(0.42f, 0.08f, 0.72f), true);
        var wax = MakeMaterial(sourceMaterial, new Color(0.55f, 0.34f, 0.18f));
        var flame = MakeMaterial(sourceMaterial, new Color(1f, 0.42f, 0.03f), true);

        var root = new GameObject("SacrificialAltar_ProceduralPrefab");
        root.transform.SetParent(resources.transform, false);
        var collider = root.AddComponent<BoxCollider>();
        collider.center = new Vector3(0f, 1.25f, 0f);
        collider.size = new Vector3(3f, 2.5f, 3f);
        if (resources.TryGetPrefabByPath(NativeSelectionPrefabKey, out var nativeSelectionPrefab))
        {
            var nativeCollider = nativeSelectionPrefab.GetComponentInChildren<Collider>(true);
            var selectionLayer = nativeCollider != null
                ? nativeCollider.gameObject.layer
                : nativeSelectionPrefab.layer;
            root.layer = selectionLayer;
            Plugin.ModLog.LogInfo(
                $"Using native furniture selection layer {selectionLayer} " +
                $"({LayerMask.LayerToName(selectionLayer)}) for the Sacrificial Altar collider.");
        }
        else
        {
            Plugin.ModLog.LogWarning(
                $"Could not find native selection prefab {NativeSelectionPrefabKey}; " +
                "the altar collider remains on the default layer.");
        }

        AddCube(root, "Stone Plinth", new Vector3(0f, .18f, 0f), new Vector3(3f, .36f, 3f), stone);
        AddCube(root, "Timber Core", new Vector3(0f, .62f, 0f), new Vector3(2.55f, .72f, 2.55f), wood);
        AddCube(root, "Stone Table", new Vector3(0f, 1.03f, 0f), new Vector3(2.9f, .22f, 2.9f), stoneEdge);
        AddCylinder(root, "Ritual Basin Rim", new Vector3(0f, 1.20f, 0f), new Vector3(2.15f, .13f, 2.15f), stoneEdge);
        AddCylinder(root, "Crimson Basin", new Vector3(0f, 1.275f, 0f), new Vector3(1.72f, .055f, 1.72f), crimson);

        var corners = new[]
        {
            new Vector3(-1.16f, 0f, -1.16f), new Vector3(1.16f, 0f, -1.16f),
            new Vector3(-1.16f, 0f, 1.16f), new Vector3(1.16f, 0f, 1.16f)
        };
        for (var index = 0; index < corners.Length; index++)
        {
            var corner = corners[index];
            AddCube(root, $"Corner Pillar {index + 1}", new Vector3(corner.x, .80f, corner.z),
                new Vector3(.48f, 1.25f, .48f), stone);
            AddCone(root, $"Horn {index + 1}", new Vector3(corner.x, 1.68f, corner.z),
                .24f, .72f, stoneEdge);
            AddCube(root, $"Violet Rune {index + 1}",
                new Vector3(corner.x, .83f, corner.z - (corner.z > 0 ? .251f : -.251f)),
                new Vector3(.12f, .42f, .025f), violet, Quaternion.Euler(0f, 0f, 35f));
        }

        AddCube(root, "Front Iron Plate", new Vector3(0f, .68f, -1.286f),
            new Vector3(.78f, .62f, .035f), iron);
        AddCube(root, "Front Violet Rune A", new Vector3(-.14f, .69f, -1.307f),
            new Vector3(.07f, .42f, .025f), violet, Quaternion.Euler(0f, 0f, 28f));
        AddCube(root, "Front Violet Rune B", new Vector3(.14f, .69f, -1.307f),
            new Vector3(.07f, .42f, .025f), violet, Quaternion.Euler(0f, 0f, -28f));

        AddCandle(root, new Vector3(-.67f, 1.38f, -.78f), .28f, wax, flame);
        AddCandle(root, new Vector3(.72f, 1.38f, .70f), .22f, wax, flame);
        AddCandle(root, new Vector3(.78f, 1.38f, -.58f), .34f, wax, flame);

        // Unlike Unity asset prefabs, this procedural template is a live scene object.
        // Keep it well outside the playable world so only instantiated entity copies
        // are visible. Leaving it at the PrefabResources origin makes it appear above
        // the school's entrance as an unclickable altar.
        root.transform.position = new Vector3(0f, -10000f, 0f);
        root.name = CustomPrefabKey;
        Plugin.ModLog.LogInfo("Built procedural Sacrificial Altar Unity prefab.");
        return root;
    }

    private static Material MakeMaterial(Material? source, Color color, bool emissive = false)
    {
        var material = source != null
            ? new Material(source)
            : new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        material.name = $"SacrificialAltar_{ColorUtility.ToHtmlStringRGB(color)}";
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        if (emissive && material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * 2.2f);
        }
        return material;
    }

    private static GameObject AddPrimitive(
        GameObject root, string name, PrimitiveType type, Vector3 position,
        Vector3 scale, Material material, Quaternion? rotation = null)
    {
        var child = GameObject.CreatePrimitive(type);
        child.name = name;
        child.transform.SetParent(root.transform, false);
        child.transform.localPosition = position;
        child.transform.localRotation = rotation ?? Quaternion.identity;
        child.transform.localScale = scale;
        child.GetComponent<Renderer>().sharedMaterial = material;
        var collider = child.GetComponent<Collider>();
        if (collider != null) Object.Destroy(collider);
        return child;
    }

    private static void AddCube(GameObject root, string name, Vector3 position,
        Vector3 scale, Material material, Quaternion? rotation = null) =>
        AddPrimitive(root, name, PrimitiveType.Cube, position, scale, material, rotation);

    private static void AddCylinder(GameObject root, string name, Vector3 position,
        Vector3 scale, Material material) =>
        AddPrimitive(root, name, PrimitiveType.Cylinder, position, scale, material);

    private static void AddCandle(GameObject root, Vector3 position, float height,
        Material wax, Material flame)
    {
        AddCylinder(root, "Candle", position + Vector3.up * (height * .5f),
            new Vector3(.14f, height * .5f, .14f), wax);
        AddPrimitive(root, "Flame", PrimitiveType.Sphere,
            position + Vector3.up * (height + .10f), new Vector3(.07f, .14f, .07f), flame);
    }

    private static void AddCone(GameObject root, string name, Vector3 position,
        float radius, float height, Material material)
    {
        const int sides = 10;
        var vertices = new Vector3[sides + 2];
        var triangles = new int[sides * 6];
        vertices[0] = new Vector3(0f, height, 0f);
        vertices[sides + 1] = Vector3.zero;
        for (var i = 0; i < sides; i++)
        {
            var angle = i * Mathf.PI * 2f / sides;
            vertices[i + 1] = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            var next = ((i + 1) % sides) + 1;
            var t = i * 6;
            triangles[t] = 0;
            triangles[t + 1] = i + 1;
            triangles[t + 2] = next;
            triangles[t + 3] = sides + 1;
            triangles[t + 4] = next;
            triangles[t + 5] = i + 1;
        }
        var mesh = new Mesh { name = name + " Mesh", vertices = vertices, triangles = triangles };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        var child = new GameObject(name);
        child.transform.SetParent(root.transform, false);
        child.transform.localPosition = position;
        child.AddComponent<MeshFilter>().sharedMesh = mesh;
        child.AddComponent<MeshRenderer>().sharedMaterial = material;
    }
}

[HarmonyPatch(typeof(PrefabResources), nameof(PrefabResources.Init))]
internal static class PrefabResourcesInitPatch
{
    [HarmonyPostfix]
    private static void BuildAltarPrefab(PrefabResources __instance) =>
        AltarPrefabPatch.EnsureBuilt(__instance);
}

[HarmonyPatch(typeof(ConfigData), nameof(ConfigData.Load))]
internal static class ArchetypeCatalogLoadPatch
{
    [System.ThreadStatic] private static bool _loadingMod;

    [HarmonyPostfix]
    private static void Register(ConfigData __instance, YamlParserType __1)
    {
        EnsureAltarPresent(__instance, __1);
    }

    internal static void EnsureAltarPresent(ConfigData catalog, YamlParserType parser)
    {
        if (_loadingMod ||
            catalog.TryGetArchetypeFromStringKey("SacrificialAltar", out _))
        {
            return;
        }

        if (!Directory.Exists(ModContent.Root))
        {
            Plugin.ModLog.LogError($"Content directory not found: {ModContent.Root}");
            return;
        }

        _loadingMod = true;
        try
        {
            var modArchetypes = new ConfigData();
            modArchetypes.Load(ModContent.Root, parser);
            foreach (var definition in modArchetypes.AllDefinitions())
            {
                catalog.Add(definition);
            }
            Plugin.ModLog.LogInfo(
                "Injected Sacrificial Altar into an archetype catalog before post-load indexing.");
        }
        catch (System.Exception exception)
        {
            Plugin.ModLog.LogError($"Failed to inject Sacrificial Altar archetype: {exception}");
        }
        finally
        {
            _loadingMod = false;
        }
    }
}

[HarmonyPatch(typeof(CharacterStatusConfigCatalog), nameof(CharacterStatusConfigCatalog.Load))]
internal static class StatusCatalogLoadPatch
{
    private static bool _injected;
    [System.ThreadStatic] private static bool _loadingMod;

    [HarmonyPostfix]
    private static void Register(CharacterStatusConfigCatalog __instance, YamlParserType __1)
    {
        if (_injected || _loadingMod) return;
        _loadingMod = true;
        try
        {
            var modCatalog = new CharacterStatusConfigCatalog();
            modCatalog.Load(ModContent.Root, __1);
            foreach (var definition in modCatalog.AllDefinitions()) __instance.Add(definition);
            DefinitionCatalog<CharacterStatusConfig>.Instance = __instance;
            _injected = true;
            Plugin.ModLog.LogInfo("Injected Sacrificed Mage status before status post-load indexing.");
        }
        catch (System.Exception exception)
        {
            Plugin.ModLog.LogError($"Failed to inject Sacrificed Mage status: {exception}");
        }
        finally
        {
            _loadingMod = false;
        }
    }
}

[HarmonyPatch(typeof(RoomTypeCatalog), nameof(RoomTypeCatalog.Load))]
internal static class RoomCatalogLoadPatch
{
    private static bool _injected;
    [System.ThreadStatic] private static bool _loadingMod;

    [HarmonyPostfix]
    private static void Register(RoomTypeCatalog __instance, YamlParserType __1)
    {
        if (_injected || _loadingMod) return;
        _loadingMod = true;
        try
        {
            var modCatalog = new RoomTypeCatalog();
            modCatalog.Load(ModContent.Root, __1);
            foreach (var definition in modCatalog.AllDefinitions()) __instance.Add(definition);
            AccessTools.Method(typeof(RoomTypeCatalog), "PostDeserialize")
                .Invoke(__instance, null);
            DefinitionCatalog<RoomTypeDefinition>.Instance = __instance;
            _injected = true;
            Plugin.ModLog.LogInfo("Injected Dark Temple and rebuilt the room-type evaluator index.");
        }
        catch (System.Exception exception)
        {
            Plugin.ModLog.LogError($"Failed to inject Dark Temple: {exception}");
        }
        finally
        {
            _loadingMod = false;
        }
    }
}

[HarmonyPatch(typeof(ConfigBundle), "PostLoad")]
internal static class WallpaperCatalogLoadPatch
{
    private static readonly List<ConfigBundle> InitializedBundles = new();

    [HarmonyPrefix]
    private static void Register(ConfigBundle __instance)
    {
        foreach (var initializedBundle in InitializedBundles)
        {
            if (ReferenceEquals(initializedBundle, __instance))
            {
                return;
            }
        }

        try
        {
            // The research screen resolves RewardKeys through the archetype catalog
            // owned by IConfigBundleProvider.ConfigBundle, not necessarily through
            // whichever ConfigData instance happened to load first. Ensure this exact
            // live catalog owns the altar before ResearchTechCatalog.Finalize runs.
            ConfigData.Instance = __instance.Archetypes;
            DefinitionCatalog<Archetype>.Instance = __instance.Archetypes;

            ArchetypeCatalogLoadPatch.EnsureAltarPresent(
                __instance.Archetypes, YamlParserType.YamlDotNet);
            ConfigData.Instance = __instance.Archetypes;
            DefinitionCatalog<Archetype>.Instance = __instance.Archetypes;

            if (!__instance.Archetypes.TryGetArchetypeFromStringKey(
                    "SacrificialAltar", out _))
            {
                throw new InvalidOperationException(
                    "The live config bundle could not resolve the Sacrificial Altar archetype.");
            }

            // Resolve the research definition only after the altar has been added.
            // Programmatic DefId references need both their key and generated UID;
            // the YAML loader normally supplies that resolution step for native data.
            EnsureSacrificialRitesResearch(__instance);

            var modCatalog = new WallpaperCatalog();
            modCatalog.Load(ModContent.Root, YamlParserType.YamlDotNet);
            foreach (var definition in modCatalog.AllDefinitions())
            {
                __instance.WallpaperCatalog.Add(definition);
            }
            DefinitionCatalog<WallpaperDefinition>.Instance =
                __instance.WallpaperCatalog;
            InitializedBundles.Add(__instance);
            Plugin.ModLog.LogInfo(
                "Injected the Dark Temple default wallpaper and validated the altar " +
                "in this config bundle's research archetype catalog before post-load.");
        }
        catch (Exception exception)
        {
            Plugin.ModLog.LogError(
                $"Failed to inject Sacrificial Altar config-bundle content: {exception}");
        }
        finally
        {
            // Temporary definition catalogs set their generic static Instance in
            // their constructors. Always return every catalog touched here to the
            // live bundle, including when an earlier injection step throws.
            ConfigData.Instance = __instance.Archetypes;
            DefinitionCatalog<Archetype>.Instance = __instance.Archetypes;
            DefinitionCatalog<ResearchTechDefinition>.Instance =
                __instance.ResearchTechCatalog;
            DefinitionCatalog<WallpaperDefinition>.Instance =
                __instance.WallpaperCatalog;
        }
    }

    private static void EnsureSacrificialRitesResearch(ConfigBundle config)
    {
        if (!config.ResearchTechCatalog.TryGetDefId("AdvancedDarkI", out var darkArtsId))
        {
            throw new InvalidOperationException("Could not resolve the Dark Arts research ID.");
        }
        if (!config.ResearchTierCatalog.TryGetDefId("Tier2", out var tier2Id))
        {
            throw new InvalidOperationException("Could not resolve the Tier II research ID.");
        }
        if (!config.Archetypes.TryGetDefId("ResearchBench", out var researchBenchId))
        {
            throw new InvalidOperationException("Could not resolve the Research Bench archetype ID.");
        }
        if (!config.Archetypes.TryGetDefId("SacrificialAltar", out var altarId))
        {
            throw new InvalidOperationException("Could not resolve the Sacrificial Altar archetype ID.");
        }
        if (!config.CodexTagCatalog.TryGetDefId("Research", out var researchTagId) ||
            !config.CodexTagCatalog.TryGetDefId("ResearchTier2", out var tier2TagId))
        {
            throw new InvalidOperationException("Could not resolve the research Codex tag IDs.");
        }

        var researchId = new DefId<ResearchTechDefinition> { Key = "SacrificialRites" };
        foreach (var existingDefinition in config.ResearchTechCatalog.AllDefinitions())
        {
            if (existingDefinition.Id.Key == researchId.Key)
            {
                // Hot reloads and multiple bundle passes may encounter an existing
                // definition. Normalize every cross-reference because a key-only
                // DefId has UID zero and will not participate in the native graph.
                existingDefinition.ResearchTier = tier2Id;
                existingDefinition.UnlockKeys = new List<DefId<ResearchTechDefinition>>
                {
                    darkArtsId
                };
                existingDefinition.ResearchedAt = new List<DefId<Archetype>>
                {
                    researchBenchId
                };
                existingDefinition.Codex ??= new CodexDescription();
                existingDefinition.Codex.Tags = new HashSet<DefId<CodexTagDefinition>>
                {
                    researchTagId,
                    tier2TagId
                };
                existingDefinition.Reward ??= new ResearchTechDefinition.UnlockReward();
                existingDefinition.Reward.RewardKeys = new List<DefId<Archetype>>
                {
                    altarId
                };
                DefinitionCatalog<ResearchTechDefinition>.Instance =
                    config.ResearchTechCatalog;
                return;
            }
        }

        var definition = new ResearchTechDefinition
        {
            Id = researchId,
            Codex = new CodexDescription
            {
                Tags = new HashSet<DefId<CodexTagDefinition>>
                {
                    researchTagId,
                    tier2TagId
                }
            },
            ResearchTier = tier2Id,
            UnlockKeys = new List<DefId<ResearchTechDefinition>>
            {
                darkArtsId
            },
            ResearchedAt = new List<DefId<Archetype>>
            {
                researchBenchId
            },
            CastsRequired = 3000,
            LayoutLocation = new ResearchTechLayoutLocation
            {
                X = 6260,
                Y = -17560,
                WidgetHeight = 320,
                WidgetWidth = 820
            },
            SubTechs = new List<DefId<ResearchTechDefinition>>(),
            Category = "Teaching",
            DisplayName = new LocalizedText
            {
                Text = "Sacrificial Rites",
                Key = "Mod.SacrificialRites.DisplayName"
            },
            FlavorText = new LocalizedText
            {
                Text = "Some knowledge demands more than study.",
                Key = "Mod.SacrificialRites.FlavorText"
            },
            AdditionalSearchKeys = new List<LocalizedText>(),
            Reward = new ResearchTechDefinition.UnlockReward
            {
                RewardKeys = new List<DefId<Archetype>>
                {
                    altarId
                },
                RewardRecipes = new List<DefId<RecipeDefinition>>(),
                RewardWallpapers = new List<DefId<WallpaperDefinition>>(),
                ResearchKeys = new List<DefId<ResearchTechDefinition>>(),
                RitualSiteConfigs = new List<RitualSiteConfig>(),
                GearSlots = new Dictionary<GearType, int>()
            }
        };

        config.ResearchTechCatalog.Add(definition);
        DefinitionCatalog<ResearchTechDefinition>.Instance =
            config.ResearchTechCatalog;

        var resolves = false;
        foreach (var storedDefinition in config.ResearchTechCatalog.AllDefinitions())
        {
            if (storedDefinition.Id.Key == researchId.Key)
            {
                resolves = true;
                break;
            }
        }

        if (!resolves)
        {
            throw new InvalidOperationException(
                "The live config bundle could not resolve Sacrificial Rites after injection.");
        }

        Plugin.ModLog.LogInfo(
            "Injected Sacrificial Rites with fully resolved prerequisite, station, tier, " +
            "Codex-tag, and altar IDs before research finalization.");
    }
}

[HarmonyPatch(typeof(ResearchTechCatalog), nameof(ResearchTechCatalog.Finalize))]
internal static class SacrificialRitesGraphValidationPatch
{
    [HarmonyPostfix]
    private static void Validate(ResearchTechCatalog __instance)
    {
        if (!__instance.TryGetDefId("AdvancedDarkI", out var darkArtsId) ||
            !__instance.TryGetDefId("SacrificialRites", out var ritesId))
        {
            Plugin.ModLog.LogError(
                "Sacrificial Rites research IDs were missing after catalog finalization.");
            return;
        }

        if (!__instance.UnlockToKeyLookup.TryGetValue(darkArtsId, out var children))
        {
            Plugin.ModLog.LogError(
                "Sacrificial Rites was not attached to Dark Arts in the finalized research graph.");
            return;
        }

        foreach (var child in children)
        {
            if (child.Id.Equals(ritesId))
            {
                Plugin.ModLog.LogInfo(
                    "Validated Sacrificial Rites as a visible child of Dark Arts in the " +
                    "finalized research graph.");
                return;
            }
        }

        Plugin.ModLog.LogError(
            "Sacrificial Rites was absent from Dark Arts' finalized child list.");
    }
}

[HarmonyPatch(typeof(BuildableUtils), nameof(BuildableUtils.GetBuildableArchetypes))]
internal static class BuildableArchetypePatch
{
    private static bool _logged;

    [HarmonyPostfix]
    private static void IncludeSacrificialAltar(ref List<Archetype> __0)
    {
        if (!ConfigData.Instance.TryGetArchetypeFromStringKey("SacrificialAltar", out var altar) ||
            __0.Contains(altar))
        {
            return;
        }

        __0.Add(altar);
        if (!_logged)
        {
            _logged = true;
            Plugin.ModLog.LogInfo("Added Sacrificial Altar to the native buildable-archetype result.");
        }
    }

}

[HarmonyPatch(typeof(View.HUDPanel_BuildPalette_Selection), "Update")]
internal static class AltarResearchVisibilityPatch
{
    private const string AltarKey = "SacrificialAltar";
    private const string RequiredResearchKey = "SacrificialRites";
    private static bool? _lastUnlockedState;

    [HarmonyPrefix]
    private static void ApplyDarkArtsGate()
    {
        var viewState = View.ViewState.Instance;
        var simulation = Simulation.Instance;
        if (viewState?.ShopConfigsByShopCategory == null ||
            simulation?.ResearchCompletion == null)
        {
            return;
        }

        var unlocked = false;
        foreach (var completedResearch in simulation.ResearchCompletion)
        {
            if (completedResearch.Key == RequiredResearchKey)
            {
                unlocked = true;
                break;
            }
        }

        if (unlocked)
        {
            RestoreAltarShopConfig(viewState);
        }
        else
        {
            RemoveAltarShopConfig(viewState);
        }

        if (_lastUnlockedState != unlocked)
        {
            _lastUnlockedState = unlocked;
            Plugin.ModLog.LogInfo(unlocked
                ? "Sacrificial Rites is complete; the Sacrificial Altar is available in the build menu."
                : "Sacrificial Rites is incomplete; the Sacrificial Altar is hidden from the build menu.");
        }
    }

    private static void RemoveAltarShopConfig(View.ViewState viewState)
    {
        foreach (var configs in viewState.ShopConfigsByShopCategory.Values)
        {
            configs.RemoveAll(config => config.EntityKey.Key == AltarKey);
        }
    }

    private static void RestoreAltarShopConfig(View.ViewState viewState)
    {
        if (!ConfigData.Instance.TryGetArchetypeFromStringKey(AltarKey, out var altar) ||
            !altar.TryGetConfig<ShopConfig>(out var shopConfig))
        {
            return;
        }

        if (!viewState.ShopConfigsByShopCategory.TryGetValue(shopConfig.Category, out var configs))
        {
            configs = new List<ShopConfig>();
            viewState.ShopConfigsByShopCategory.Add(shopConfig.Category, configs);
        }

        foreach (var config in configs)
        {
            if (config.EntityKey.Key == AltarKey)
            {
                return;
            }
        }

        configs.Add(shopConfig);
    }
}

[HarmonyPatch(typeof(View.ArtifactSlotWidget), "GetArtifactPickerUse")]
internal static class AltarRelicPickerPatch
{
    [HarmonyPostfix]
    private static void EnableExistingRelicPicker(
        RitualSiteComponent? ____ritualSite,
        ref View.ArtifactPickerUse __result)
    {
        if (____ritualSite?.Entity.EntityKey.Key == "SacrificialAltar" &&
            __result == View.ArtifactPickerUse.None)
        {
            __result = View.ArtifactPickerUse.ChangeLegacy;
        }
    }
}

[HarmonyPatch]
internal static class AltarRelicCandidatePatch
{
    private static MethodBase TargetMethod()
    {
        var closureType = AccessTools.Inner(
            typeof(View.ArtifactPickerHelper),
            "<>c__DisplayClass0_0");
        return AccessTools.Method(
            closureType,
            "<ShowPickerForArtifactSlotSelection>b__0");
    }

    [HarmonyPostfix]
    private static void SupplySchoolRelics(
        object __instance,
        ref List<ValueTuple<
            ArtifactComponent,
            DefId<LegacyTypesDefinition>,
            bool,
            bool,
            bool,
            bool>> __result)
    {
        var closureType = __instance.GetType();
        var ritualSite = AccessTools.Field(closureType, "ritualSite")
            .GetValue(__instance) as RitualSiteComponent;
        if (ritualSite?.Entity.EntityKey.Key != "SacrificialAltar")
        {
            return;
        }

        var selectedArtifact = AccessTools.Field(closureType, "selectedArtifact")
            .GetValue(__instance) as ArtifactComponent;
        var relics = new List<ValueTuple<
            ArtifactComponent,
            DefId<LegacyTypesDefinition>,
            bool,
            bool,
            bool,
            bool>>();

        foreach (var artifact in ComponentManager.Instance.InSchoolEnumerator<ArtifactComponent>())
        {
            var isAvailable = artifact.AssignedTo == null ||
                ReferenceEquals(artifact, selectedArtifact) ||
                (artifact.AssignmentIsProvisional &&
                 ReferenceEquals(artifact.AssignedTo, ritualSite.Entity));
            relics.Add(new ValueTuple<
                ArtifactComponent,
                DefId<LegacyTypesDefinition>,
                bool,
                bool,
                bool,
                bool>(
                artifact,
                default,
                isAvailable,
                true,
                true,
                false));
        }

        __result = relics;
    }
}

[HarmonyPatch(typeof(RitualSiteComponent), nameof(RitualSiteComponent.RitualCompleted))]
internal static class SacrificeCompletionPatch
{
    private sealed class CompletionState
    {
        internal RitualSiteComponent RitualSite = null!;
        internal ArtifactComponent Relic = null!;
        internal int Increase;
        internal string SacrificeName = string.Empty;
    }

    [HarmonyPrefix]
    private static void Capture(RitualSiteComponent __instance, ref CompletionState? __state)
    {
        if (__instance.Entity.EntityKey.Key != "SacrificialAltar" ||
            !__instance.TryGetInputArtifact(out var artifact))
        {
            return;
        }

        Entity? sacrifice = null;
        foreach (var attendee in __instance.RitualAttendees)
        {
            if (attendee.Item2 == 0)
            {
                sacrifice = attendee.Item1;
                break;
            }
        }

        if (sacrifice is null)
        {
            Plugin.ModLog.LogError("Sacrificial ritual completed without a sacrifice participant.");
            return;
        }

        var increase = sacrifice.HasComponent<StaffComponent>() ? 3
            : sacrifice.HasComponent<ApprenticeComponent>() ? 2
            : 1;

        if (RoomUtils.TryFindRoom(__instance.Entity, out var room))
        {
            var roomComponent = RoomUtils.GetRoomComponent(room);
            if (roomComponent.RoomType?.Id.Key == "DarkTemple")
            {
                increase += 1;
            }
        }

        // Basic rituals normally consume every delivered input. A relic is a
        // target of this ritual, not a payment, so detach it from the delivery
        // list before the game's generic completion cleanup runs.
        foreach (var ritualErrand in __instance.Entity.AllComponents<RitualErrand>())
        {
            if (ReferenceEquals(ritualErrand.RitualSite, __instance))
            {
                ritualErrand.DeliveredInputs?.Remove(artifact.Entity);
            }
        }

        __state = new CompletionState
        {
            RitualSite = __instance,
            Relic = artifact,
            Increase = increase,
            SacrificeName = EntityUtils.GetDisplayName(sacrifice)
        };
    }

    [HarmonyPostfix]
    private static void Upgrade(CompletionState? __state)
    {
        if (__state is null)
        {
            return;
        }

        var ritualSite = __state.RitualSite;
        var artifact = __state.Relic;
        if (artifact.IsDestroyed || artifact.Entity.IsDestroyed)
        {
            Plugin.ModLog.LogError(
                $"The relic selected for {__state.SacrificeName}'s sacrifice was destroyed " +
                "during ritual completion; no level-cap increase was applied.");
            return;
        }

        var actualIncrease = RelicLevelUtils.UpgradeLevelCap(
            artifact.Entity,
            __state.Increase);

        // Release the upgraded relic from the altar and return it to the world,
        // matching the native post-ritual behavior used for relic modification.
        if (ritualSite.Entity.TryGetComponent<RitualSiteRelicComponent>(out var relicSlot) &&
            ReferenceEquals(relicSlot.InputArtifact, artifact))
        {
            relicSlot.InputArtifact = null;
        }
        ArtifactUtils.ClearArtifactAssignment(artifact, ritualSite.Entity);
        artifact.EquippedBy = null;
        artifact.Entity.RemoveComponentsOfType<DeliveredComponent>();
        artifact.Entity.RemoveComponentsOfType<UnavailableResourceComponent>();
        if (ritualSite.Entity.TryGetComponent<TransformComponent>(out var altarTransform))
        {
            try
            {
                // Simulation.dll exposes its position through Unity's legacy
                // System.Numerics assembly, which cannot be referenced directly
                // by this netstandard plugin. Invoke the native placement API
                // without changing or converting the game's vector value.
                var placementMethod = Array.Find(
                    typeof(EntityPlacementUtils).GetMethods(
                        BindingFlags.Public | BindingFlags.Static),
                    method => method.Name == "EntityPlacementRequest" &&
                        method.GetParameters().Length == 3);
                var positionProperty = AccessTools.Property(
                    typeof(TransformComponent),
                    "Position");
                if (placementMethod != null && positionProperty != null)
                {
                    placementMethod.Invoke(
                        null,
                        new object[]
                        {
                            artifact.Entity,
                            positionProperty.GetValue(altarTransform),
                            EntityPlacementRequestData.Mode.Nearby
                        });
                }
                else
                {
                    Plugin.ModLog.LogWarning(
                        "Could not locate the native relic-placement API; " +
                        "the upgraded relic was preserved at the altar.");
                }
            }
            catch (Exception exception)
            {
                Plugin.ModLog.LogError(
                    $"The upgraded relic was preserved but could not be placed " +
                    $"beside the altar: {exception}");
            }
        }

        Plugin.ModLog.LogInfo(
            $"Sacrificed {__state.SacrificeName}; requested +{__state.Increase} relic cap " +
            $"and applied +{actualIncrease}.");
    }
}
