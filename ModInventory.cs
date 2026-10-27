using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;

namespace Silverpine.ModdingTools;

/// <summary>A loaded plugin or installed preloader assembly. Patcher versions describe disk contents, not execution state.</summary>
public sealed class InstalledModInfo
{
    public InstalledModInfo(string id, string name, Version version, string kind)
    { Id = id; Name = name; Version = version; Kind = kind; }
    public string Id { get; }
    public string Name { get; }
    public Version Version { get; }
    public string Kind { get; }
}

public sealed class ModInventorySnapshot
{
    internal ModInventorySnapshot(List<InstalledModInfo> mods, List<string> warnings)
    { Mods = mods.AsReadOnly(); Warnings = warnings.AsReadOnly(); }
    public IReadOnlyList<InstalledModInfo> Mods { get; }
    public IReadOnlyList<string> Warnings { get; }
}

/// <summary>On-demand local inventory. Does not load/execute patcher DLLs or make network requests.</summary>
public static class ModInventory
{
    /// <summary>Call on Unity's main thread. Includes all loaded BepInEx identities, including compatibility aliases.</summary>
    public static ModInventorySnapshot Capture()
    {
        var mods = Chainloader.PluginInfos.Values.Select(info => new InstalledModInfo(
            info.Metadata.GUID, info.Metadata.Name, info.Metadata.Version, "plugin")).ToList();
        var warnings = new List<string>();
        ReadPatchers(Paths.PatcherPluginPath, mods, warnings);
        return new ModInventorySnapshot(mods.OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase).ToList(), warnings);
    }

    internal static void ReadPatchers(string directory, List<InstalledModInfo> mods, List<string> warnings)
    {
        if (!Directory.Exists(directory)) return;
        try
        {
            foreach (string path in Directory.EnumerateFiles(directory, "*.dll", SearchOption.AllDirectories))
            {
                try
                {
                    // AssemblyName reads only the manifest; this must never use Assembly.LoadFrom.
                    var assembly = AssemblyName.GetAssemblyName(path);
                    mods.Add(new InstalledModInfo("assembly:" + assembly.Name, assembly.Name!,
                        assembly.Version ?? new Version(0, 0), "patcher"));
                }
                catch (Exception ex)
                {
                    warnings.Add("Could not read patcher " + Path.GetFileName(path) + ": " + ex.Message);
                }
            }
        }
        catch (Exception ex) { warnings.Add("Patcher inventory incomplete: " + ex.Message); }
    }
}
