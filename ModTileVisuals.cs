#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Silverpine.ModdingTools;

/// <summary>On-demand grass, water-edge, and abyss/wall-face refreshes for loaded turf coordinates.</summary>
public static class ModTileVisuals
{
    private const int MaximumTiles = 65536;
    private const string HarmonyId = Plugin.PluginGuid + ".feature.visuals.tiles";
    private static readonly MethodInfo? WaterInitialize = AccessTools.Method(typeof(DeepTileAOHandler), "Initialize");
    private sealed class WaterEdges { internal readonly HashSet<GameObject> Objects = new(); }
    private static readonly ConditionalWeakTable<DeepTileAOHandler, WaterEdges> waterEdges = new();
    private static WaterEdges EdgesFor(DeepTileAOHandler owner) => waterEdges.GetValue(owner, _ => new WaterEdges());
    private static HashSet<Vector2Int> pending = new();
    private static MonoBehaviour? host;
    private static Coroutine? queued;
    private static int mainThreadId;
    private static int lastRequestFrame;
    private static bool initialized;

    internal static void Initialize()
    {
        if (initialized) return;
        if (WaterInitialize == null) throw new MissingMethodException(typeof(DeepTileAOHandler).FullName, "Initialize");
        mainThreadId = Thread.CurrentThread.ManagedThreadId;
        var harmony = new Harmony(HarmonyId);
        try
        {
            harmony.CreateClassProcessor(typeof(TileVisualWaterTrackingPatch)).Patch();
            harmony.CreateClassProcessor(typeof(TileVisualLoadResetPatch)).Patch();
            harmony.CreateClassProcessor(typeof(TileVisualUnloadResetPatch)).Patch();
            SceneManager.activeSceneChanged += OnSceneChanged;
            initialized = true;
        }
        catch
        {
            harmony.UnpatchSelf();
            host = null;
            throw;
        }
    }

    internal static void Shutdown()
    {
        ClearPending();
        SceneManager.activeSceneChanged -= OnSceneChanged;
        new Harmony(HarmonyId).UnpatchSelf();
        host = null;
        initialized = false;
    }

    private static void EnsureReady()
    {
        if (Thread.CurrentThread.ManagedThreadId != mainThreadId)
            throw new InvalidOperationException("Tile visuals must be refreshed on Unity's main thread.");
        if (!initialized || !FrameworkDiagnostics.IsFeatureAvailable("visuals.tiles"))
            throw new InvalidOperationException("The tile-visual refresh service is unavailable. Check Framework Status.");
        if (host == null)
        {
            // Silverpine can also destroy DontDestroyOnLoad objects during
            // bootstrap. A dead coroutine host is not a failed API feature.
            ClearPending();
            host = FrameworkRuntime.EnsureHost();
        }
    }

    /// <summary>Refresh now, after tile registration/Start. Returns successfully refreshed component count.</summary>
    public static int Refresh(Vector2Int tile, bool includeNeighbors = true) =>
        Refresh(new[] { tile }, includeNeighbors);

    /// <summary>Refresh a deduplicated batch now. Neighbors include all eight surrounding cells.</summary>
    public static int Refresh(IEnumerable<Vector2Int> tiles, bool includeNeighbors = true)
    {
        EnsureReady();
        return RefreshPositions(BuildAffected(tiles, includeNeighbors));
    }

    /// <summary>Refresh a rectangle; xMax/yMax are exclusive. Empty rectangles do nothing.</summary>
    public static int RefreshArea(RectInt area, bool includeNeighbors = true) =>
        Refresh(AreaPositions(area), includeNeighbors);

    /// <summary>Recommended after spawning/deleting tiles. Coalesces requests and waits two frames after the last request.</summary>
    public static void RequestRefresh(Vector2Int tile, bool includeNeighbors = true) =>
        RequestRefresh(new[] { tile }, includeNeighbors);

    /// <summary>Queue a batch without scanning the world. Pending work is discarded on load, world unload, or scene change.</summary>
    public static void RequestRefresh(IEnumerable<Vector2Int> tiles, bool includeNeighbors = true)
    {
        EnsureReady();
        HashSet<Vector2Int> affected = BuildAffected(tiles, includeNeighbors);
        if (affected.Count == 0) return;
        // Validate the entire union before changing the queue.
        affected.UnionWith(pending);
        if (affected.Count > MaximumTiles) throw new ArgumentException("Pending tile refresh exceeds 65,536 cells.", nameof(tiles));
        pending = affected;
        lastRequestFrame = Time.frameCount;
        if (queued == null) queued = host!.StartCoroutine(FlushWhenReady());
    }

    public static void RequestRefreshArea(RectInt area, bool includeNeighbors = true) =>
        RequestRefresh(AreaPositions(area), includeNeighbors);

    internal static HashSet<Vector2Int> BuildAffected(IEnumerable<Vector2Int> tiles, bool includeNeighbors)
    {
        if (tiles == null) throw new ArgumentNullException(nameof(tiles));
        var result = new HashSet<Vector2Int>();
        int count = 0, radius = includeNeighbors ? 1 : 0;
        foreach (Vector2Int tile in tiles)
        {
            if (++count > MaximumTiles) throw new ArgumentException("A refresh batch may contain at most 65,536 input cells.", nameof(tiles));
            for (int dx = -radius; dx <= radius; dx++)
                for (int dy = -radius; dy <= radius; dy++)
                {
                    long x = (long)tile.x + dx, y = (long)tile.y + dy;
                    if (x < int.MinValue || x > int.MaxValue || y < int.MinValue || y > int.MaxValue) continue;
                    result.Add(new Vector2Int((int)x, (int)y));
                }
            if (result.Count > MaximumTiles) throw new ArgumentException("A refresh batch plus neighbors may cover at most 65,536 cells.", nameof(tiles));
        }
        return result;
    }

    internal static IEnumerable<Vector2Int> AreaPositions(RectInt area)
    {
        if (area.width < 0 || area.height < 0 || (long)area.width * area.height > MaximumTiles)
            throw new ArgumentOutOfRangeException(nameof(area), "Use a nonnegative rectangle covering at most 65,536 cells.");
        long right = (long)area.x + area.width, top = (long)area.y + area.height;
        if (right > (long)int.MaxValue + 1 || top > (long)int.MaxValue + 1)
            throw new ArgumentOutOfRangeException(nameof(area), "The rectangle exceeds the integer coordinate range.");
        if (area.width == 0 || area.height == 0) yield break;
        for (long x = area.x; x < right; x++)
            for (long y = area.y; y < top; y++)
                yield return new Vector2Int((int)x, (int)y);
    }

    internal static bool ShouldFlush(int frame, int lastRequest) => unchecked((uint)(frame - lastRequest)) >= 2u;

    private static IEnumerator FlushWhenReady()
    {
        do { yield return null; } while (!ShouldFlush(Time.frameCount, lastRequestFrame));
        HashSet<Vector2Int> batch = pending;
        pending = new();
        queued = null;
        RefreshPositions(batch);
    }

    internal static void ClearPending()
    {
        if (host != null && queued != null) host.StopCoroutine(queued);
        queued = null;
        pending.Clear();
    }

    internal static void ReleaseHost(FrameworkRuntime owner)
    {
        if (!ReferenceEquals(host, owner)) return;
        // Unity stops this object's coroutines on destruction. Forget their
        // handles, but keep process-wide patches and water ownership tracking.
        host = null;
        queued = null;
        pending.Clear();
    }

    private static void OnSceneChanged(Scene previous, Scene current) => ClearPending();

    private static int RefreshPositions(IEnumerable<Vector2Int> positions)
    {
        var seen = new HashSet<Component>();
        int refreshed = 0;
        foreach (Vector2Int position in positions)
        {
            if (!Turfs.TryGetTurfGameObjects(position, out var objects)) continue;
            foreach (GameObject item in objects.ToArray())
            {
                if (item == null || !item.activeInHierarchy) continue;
                foreach (OvergrowthTile grass in item.GetComponents<OvergrowthTile>())
                {
                    if (grass == null || !grass.isActiveAndEnabled || !seen.Add(grass)) continue;
                    try { grass.Refresh(); refreshed++; }
                    catch (Exception ex) { FrameworkDiagnostics.Report(Plugin.PluginGuid, "refresh grass edges at " + position, ex); }
                }
                foreach (DeepTileAOHandler water in item.GetComponents<DeepTileAOHandler>())
                {
                    if (water == null || !water.isActiveAndEnabled || !seen.Add(water)) continue;
                    try { if (RefreshWater(water)) refreshed++; }
                    catch (Exception ex) { FrameworkDiagnostics.Report(Plugin.PluginGuid, "refresh water edges at " + position, ex.InnerException ?? ex); }
                }
                foreach (PerspectiveWallHandler edge in item.GetComponents<PerspectiveWallHandler>())
                {
                    if (edge == null || !edge.isActiveAndEnabled || !seen.Add(edge)) continue;
                    try { edge.UpdateSpriteImmediately(); refreshed++; }
                    catch (Exception ex) { FrameworkDiagnostics.Report(Plugin.PluginGuid, "refresh abyss/wall face at " + position, ex); }
                }
            }
        }
        return refreshed;
    }

    private static bool RefreshWater(DeepTileAOHandler owner)
    {
        // If native Start has not run yet, leave it to initialize itself. The
        // deferred API waits for this, rather than causing a second initialization.
        if (!waterEdges.TryGetValue(owner, out WaterEdges? owned) || owned == null) return false;
        foreach (GameObject edge in owned.Objects)
        {
            if (edge == null || edge.transform.parent != owner.transform) continue;
            edge.SetActive(false);
            UnityEngine.Object.Destroy(edge);
        }
        owned.Objects.Clear();
        WaterInitialize!.Invoke(owner, null);
        return true;
    }

    internal static HashSet<GameObject> BeforeWaterInitialization(DeepTileAOHandler owner)
    {
        EdgesFor(owner);
        var before = new HashSet<GameObject>();
        foreach (Transform child in owner.transform) before.Add(child.gameObject);
        return before;
    }

    internal static void AfterWaterInitialization(DeepTileAOHandler owner, HashSet<GameObject>? before)
    {
        if (owner == null || before == null) return;
        WaterEdges owned = EdgesFor(owner);
        foreach (Transform child in owner.transform)
            if (!before.Contains(child.gameObject) && IsNativeWaterEdgeName(child.name)) owned.Objects.Add(child.gameObject);
    }

    internal static bool IsNativeWaterEdgeName(string name) =>
        name == "prefab_effect_fakeao_water(Clone)" || name == "prefab_effect_fakeao_water_corner(Clone)";
}

[HarmonyPatch(typeof(DeepTileAOHandler), "Initialize")]
internal static class TileVisualWaterTrackingPatch
{
    private static void Prefix(DeepTileAOHandler __instance, out HashSet<GameObject>? __state)
    {
        __state = null;
        try { __state = ModTileVisuals.BeforeWaterInitialization(__instance); }
        catch (Exception ex) { FrameworkDiagnostics.Report(Plugin.PluginGuid, "track native water edges", ex); }
    }

    // Track partial spawns even if native initialization fails, without suppressing its exception.
    private static void Finalizer(DeepTileAOHandler __instance, HashSet<GameObject>? __state) =>
        FrameworkDiagnostics.Invoke(Plugin.PluginGuid, "track native water edges", () =>
            ModTileVisuals.AfterWaterInitialization(__instance, __state));
}

[HarmonyPatch(typeof(SerializationManager), nameof(SerializationManager.Load))]
internal static class TileVisualLoadResetPatch
{
    private static void Prefix() => ModTileVisuals.ClearPending();
}

[HarmonyPatch(typeof(WorldTile), nameof(WorldTile.Unload))]
internal static class TileVisualUnloadResetPatch
{
    private static void Prefix() => ModTileVisuals.ClearPending();
}
