using HarmonyLib;
using Silverpine.ModdingTools;
using UnityEngine;
using UnityEngine.SceneManagement;
using UObject = UnityEngine.Object;

// Executes the production service/host code with a minimal fake Unity lifecycle.
// This is not an in-game visual or Harmony-patching test.
int passed = 0;
void Check(bool value, string message)
{
    if (!value) throw new Exception(message);
    Console.WriteLine("PASS " + message);
    passed++;
}
void Tick(int count = 1) { for (int i = 0; i < count; i++) TestClock.Tick(); }
bool Throws(Action action)
{
    try { action(); return false; }
    catch (InvalidOperationException) { return true; }
}
var tile = new Vector2Int(5, 8);
var ground = new GameObject("grass");
var grass = ground.AddComponent<OvergrowthTile>();
Turfs.Cells[tile] = new List<GameObject> { ground };
var adjacent = new GameObject("neighbor");
var neighbor = adjacent.AddComponent<OvergrowthTile>();
Turfs.Cells[new Vector2Int(6, 8)] = new List<GameObject> { adjacent };

FrameworkRuntime.Initialize();
var bootstrap = FrameworkRuntime.EnsureHost();
ModTileVisuals.Initialize();
ModTileVisuals.Initialize();
Check(Harmony.Patches == 3, "process hooks initialize once, not once per consumer");
UObject.Destroy(bootstrap.gameObject);
Check(FrameworkDiagnostics.Available && Harmony.Unpatches == 0,
    "bootstrap host destruction does not disable the API or remove its patches");

// Consumer A and B overlap. No scene callback is required for recovery.
void ConsumerA() => ModTileVisuals.RequestRefresh(tile);
void ConsumerB() => ModTileVisuals.RequestRefresh(new[] { tile, new Vector2Int(6, 8) });
ConsumerA();
var recovered = FrameworkRuntime.EnsureHost();
ConsumerB();
Check(!ReferenceEquals(recovered, bootstrap) && recovered != null && TestClock.Active == 1,
    "first consumer recreates a destroyed host; the second shares its coroutine");
Tick();
Check(grass.Refreshes == 0, "coalesced work does not execute before two frames");
Tick();
Check(grass.Refreshes == 1 && neighbor.Refreshes == 1 && TestClock.Active == 0,
    "overlapping consumer requests refresh each component once and become idle");

ConsumerA(); Tick(); ConsumerB(); Tick();
Check(grass.Refreshes == 1, "a later consumer request extends the settling window");
Tick();
Check(grass.Refreshes == 2, "the combined batch executes after the latest request settles");

ConsumerA();
UObject.Destroy(recovered!.gameObject);
Tick(3);
Check(grass.Refreshes == 2 && TestClock.Active == 0, "host destruction abandons its queued batch");
SceneManager.LoadScene();
var sceneHost = FrameworkRuntime.EnsureHost();
Check(sceneHost != null && !ReferenceEquals(sceneHost, recovered), "scene load restores the runtime");
ConsumerB(); Tick(2);
Check(grass.Refreshes == 3 && Harmony.Patches == 3 && Harmony.Unpatches == 0,
    "new requests flush after recovery without a stale coroutine handle or duplicate hooks");

// Exercise the defensive fake-null path even if OnDestroy was not delivered.
ConsumerA();
TestClock.Abandon(sceneHost!);
ConsumerB(); Tick(2);
Check(grass.Refreshes == 4, "API recovery also clears stale handles when no destroy callback was delivered");

ConsumerA();
SceneManager.ChangeScene(); Tick(2);
Check(grass.Refreshes == 4, "scene changes cancel old-world pending work");
ConsumerB(); Tick(2);
Check(grass.Refreshes == 5, "requests after scene cancellation can start a new coroutine");

// Track a native-owned edge before host replacement, as the native hook does.
var waterObject = new GameObject("water");
var water = waterObject.AddComponent<DeepTileAOHandler>();
var before = ModTileVisuals.BeforeWaterInitialization(water);
var oldEdge = new GameObject("prefab_effect_fakeao_water(Clone)");
oldEdge.transform.parent = waterObject.transform;
var decoration = new GameObject("mod decoration");
decoration.transform.parent = waterObject.transform;
ModTileVisuals.AfterWaterInitialization(water, before);
Turfs.Cells[new Vector2Int(7, 8)] = new List<GameObject> { waterObject };
UObject.Destroy(FrameworkRuntime.EnsureHost().gameObject);
int components = ModTileVisuals.Refresh(new Vector2Int(7, 8), false);
Check(components == 1 && oldEdge == null && decoration != null && water.Initializations == 1,
    "immediate refresh recovers the host and retains owned-water cleanup without deleting other children");

var offThread = new Thread(() => Check(Throws(ConsumerA), "off-main-thread requests are still rejected"));
offThread.Start(); offThread.Join();
FrameworkDiagnostics.Available = false;
Check(Throws(ConsumerA), "an actual feature-initialization failure is not disguised by host recovery");
FrameworkDiagnostics.Available = true;
Check(FrameworkDiagnostics.Errors.Count == 0, "normal lifecycle recovery raises no diagnostic errors");

var quittingHost = FrameworkRuntime.EnsureHost();
TestClock.Invoke(quittingHost, "OnApplicationQuit");
UObject.Destroy(quittingHost.gameObject);
int created = GameObject.Created;
SceneManager.LoadScene();
Check(Throws(ConsumerA) && GameObject.Created == created, "shutdown never respawns a runtime");
Console.WriteLine($"{passed} simulated host/queue checks passed. In-game validation is still required.");
