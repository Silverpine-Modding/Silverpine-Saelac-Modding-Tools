// Deliberately small test doubles. No game assemblies, save data, or Unity process are used.
using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace UnityEngine
{
    public class Object
    {
        internal bool Destroyed;
        public static bool operator ==(Object? left, Object? right) =>
            ReferenceEquals(left, right) || ((ReferenceEquals(left, null) || left.Destroyed) &&
                (ReferenceEquals(right, null) || right.Destroyed));
        public static bool operator !=(Object? left, Object? right) => !(left == right);
        public override bool Equals(object? other) => ReferenceEquals(this, other);
        public override int GetHashCode() => RuntimeHelpers.GetHashCode(this);
        public static void DontDestroyOnLoad(Object value) { }
        public static void Destroy(Object value)
        {
            if (value is GameObject go)
            {
                foreach (Component component in go.Components)
                {
                    if (component is MonoBehaviour behaviour) TestClock.Abandon(behaviour);
                    TestClock.Invoke(component, "OnDestroy");
                    component.Destroyed = true;
                }
                go.transform.parent = null;
            }
            value.Destroyed = true;
        }
    }
    public class Component : Object
    {
        public GameObject gameObject = null!;
        public Transform transform => gameObject.transform;
    }
    public class MonoBehaviour : Component
    {
        public bool isActiveAndEnabled => !Destroyed && gameObject.activeInHierarchy;
        public Coroutine StartCoroutine(IEnumerator iterator) => TestClock.Start(this, iterator);
        public void StopCoroutine(Coroutine coroutine) => TestClock.Stop(coroutine);
    }
    public class Coroutine
    {
        internal MonoBehaviour Owner = null!;
        internal IEnumerator Iterator = null!;
    }
    public class GameObject : Object
    {
        public static int Created;
        internal readonly List<Component> Components = new();
        public string name;
        public Transform transform;
        public bool activeInHierarchy = true;
        public GameObject(string name) { Created++; this.name = name; transform = new Transform(this); }
        public void SetActive(bool active) => activeInHierarchy = active;
        public T AddComponent<T>() where T : Component, new()
        {
            var component = new T { gameObject = this };
            Components.Add(component);
            TestClock.Invoke(component, "Awake");
            return component;
        }
        public T[] GetComponents<T>() => Components.OfType<T>().ToArray();
    }
    public class Transform : IEnumerable<Transform>
    {
        public readonly GameObject gameObject;
        public string name => gameObject.name;
        private Transform? owner;
        private readonly List<Transform> children = new();
        public Transform(GameObject gameObject) => this.gameObject = gameObject;
        public Transform? parent
        {
            get => owner;
            set { owner?.children.Remove(this); owner = value; owner?.children.Add(this); }
        }
        public IEnumerator<Transform> GetEnumerator() => children.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
    public readonly record struct Vector2Int(int x, int y);
    public readonly record struct RectInt(int x, int y, int width, int height);
    public static class Time { public static int frameCount; public static float unscaledTime; }
    public static class TestClock
    {
        private static readonly List<Coroutine> running = new();
        public static int Active => running.Count;
        public static void Invoke(object instance, string method) => instance.GetType()
            .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(instance, null);
        public static Coroutine Start(MonoBehaviour owner, IEnumerator iterator)
        {
            var coroutine = new Coroutine { Owner = owner, Iterator = iterator };
            if (iterator.MoveNext()) running.Add(coroutine);
            return coroutine;
        }
        public static void Stop(Coroutine coroutine) => running.Remove(coroutine);
        public static void Abandon(MonoBehaviour owner)
        {
            owner.Destroyed = true;
            running.RemoveAll(c => ReferenceEquals(c.Owner, owner));
        }
        public static void Tick()
        {
            Time.frameCount++; Time.unscaledTime += 1f / 60;
            foreach (var coroutine in running.ToArray())
                if (coroutine.Owner == null || !coroutine.Iterator.MoveNext()) running.Remove(coroutine);
        }
    }
}
namespace UnityEngine.SceneManagement
{
    public struct Scene { }
    public enum LoadSceneMode { Single }
    public static class SceneManager
    {
        public static event Action<Scene, Scene>? activeSceneChanged;
        public static event Action<Scene, LoadSceneMode>? sceneLoaded;
        public static void ChangeScene() => activeSceneChanged?.Invoke(default, default);
        public static void LoadScene() => sceneLoaded?.Invoke(default, default);
    }
}
namespace HarmonyLib
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class HarmonyPatch : Attribute { public HarmonyPatch(Type type, string name) { } }
    public sealed class Harmony
    {
        public static int Patches, Unpatches;
        public Harmony(string id) { }
        public Harmony CreateClassProcessor(Type type) => this;
        public void Patch() => Patches++;
        public void UnpatchSelf() => Unpatches++;
    }
    public static class AccessTools
    {
        public static MethodInfo? Method(Type type, string name) => type.GetMethod(name,
            BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
    }
}
namespace Silverpine.ModdingTools
{
    public static class Plugin { public const string PluginGuid = "tests.framework"; }
    public static class FrameworkDiagnostics
    {
        public static bool Available = true;
        public static readonly List<Exception> Errors = new();
        public static bool IsFeatureAvailable(string id) => Available;
        public static void Report(string owner, string operation, Exception ex) => Errors.Add(ex);
        public static void Invoke(string owner, string operation, Action action)
        {
            try { action(); } catch (Exception ex) { Report(owner, operation, ex); }
        }
    }
    public static class ModSaveData { public static void ShowPendingWarning() { } }
}
public static class Turfs
{
    public static readonly Dictionary<UnityEngine.Vector2Int, List<UnityEngine.GameObject>> Cells = new();
    public static bool TryGetTurfGameObjects(UnityEngine.Vector2Int position, out List<UnityEngine.GameObject> objects) =>
        Cells.TryGetValue(position, out objects!);
}
public sealed class OvergrowthTile : UnityEngine.MonoBehaviour
{
    public int Refreshes;
    public void Refresh() => Refreshes++;
}
public sealed class DeepTileAOHandler : UnityEngine.MonoBehaviour
{
    public int Initializations;
    private void Initialize() => Initializations++;
}
public sealed class PerspectiveWallHandler : UnityEngine.MonoBehaviour { public void UpdateSpriteImmediately() { } }
public static class SerializationManager { public static void Load() { } }
public class WorldTile { public void Unload() { } }
