#nullable enable
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace Silverpine.ModdingTools;

// Native floor handlers run for construction and deconstruction, including
// floors placed by mods. Arbitrary furniture and preview movement do not trigger this.
[HarmonyPatch]
internal static class FloorTileVisualRefreshPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(GrassTileHandler), nameof(GrassTileHandler.OnConstructed));
        yield return AccessTools.Method(typeof(GrassTileHandler), nameof(GrassTileHandler.OnDeconstructed));
    }

    private static void Postfix(GrassTileHandler __instance)
    {
        // OnDeconstructed runs before the old floor is destroyed. The queued
        // API lets destruction and the restored ground's Start complete first.
        FrameworkDiagnostics.Invoke(Plugin.PluginGuid, "refresh constructed/deconstructed floor", () =>
            ModTileVisuals.RequestRefresh(__instance.transform.GetVector2IntPosition(), includeNeighbors: true));
    }
}
