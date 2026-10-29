#nullable enable
using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Silverpine.ModdingTools;

// The service registrations are process-wide; this Unity object is replaceable.
// DontDestroyOnLoad alone does not protect against Silverpine's bootstrap cleanup.
internal sealed class FrameworkRuntime : MonoBehaviour
{
    private static FrameworkRuntime? instance;
    private static bool subscribed;
    private static bool quitting;
    private float nextCheck;

    internal static void Initialize()
    {
        if (!subscribed)
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            subscribed = true;
        }
        EnsureHost();
    }

    internal static FrameworkRuntime EnsureHost()
    {
        if (quitting) throw new InvalidOperationException("Modding Tools is shutting down.");
        if (instance != null) return instance;
        var root = new GameObject("ModdingToolsFrameworkRuntime");
        DontDestroyOnLoad(root);
        instance = root.AddComponent<FrameworkRuntime>();
        return instance;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!quitting)
            FrameworkDiagnostics.Invoke(Plugin.PluginGuid, "restore framework runtime", () => EnsureHost());
    }

    private void Awake() => instance = this;

    private void OnApplicationQuit() => quitting = true;

    private void OnDestroy()
    {
        // Object-local cleanup only. Never remove persistent API hooks here,
        // and never create a replacement from inside Unity's destruction loop.
        ModTileVisuals.ReleaseHost(this);
        if (ReferenceEquals(instance, this)) instance = null;
    }

    private void Update()
    {
        if (Time.unscaledTime < nextCheck) return;
        nextCheck = Time.unscaledTime + 0.5f;
        FrameworkDiagnostics.Invoke(Plugin.PluginGuid, "save warning display", ModSaveData.ShowPendingWarning);
    }
}
