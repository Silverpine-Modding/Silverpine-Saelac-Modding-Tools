#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Silverpine.ModdingTools;

/// <summary>Framework-owned, content-addressed artwork for any mod. No Custom Item Loader dependency.</summary>
public static class SharedArtApi
{
    public static IReadOnlyList<SharedArtInfo> GetEntries() => SharedArtLibrary.ReadEntries(
        message => Plugin.Log.LogWarning("Shared art: " + message)).Select(e => new SharedArtInfo(e)).ToArray();

    public static string GetViewPath(string id, int view = 0)
    {
        SharedArtLibrary.Entry entry = Find(id);
        if (view < 0 || view >= entry.views.Length) throw new ArgumentOutOfRangeException(nameof(view));
        return SharedArtLibrary.Resolve(entry.views[view]);
    }

    /// <summary>Copies one view into the destination pack; no dependency on the library remains.</summary>
    public static string CopyViewToPack(string id, string packDirectory, int view = 0) =>
        ImportImage(GetViewPath(id, view), packDirectory);

    /// <summary>Never replaces existing assets, even when source basenames collide.</summary>
    public static string ImportImage(string source, string packDirectory)
    {
        string extension = Path.GetExtension(source).ToLowerInvariant();
        if (extension != ".png" && extension != ".jpg" && extension != ".jpeg")
            throw new InvalidDataException("Image must be PNG or JPEG.");
        string path = SharedArtFiles.ImportImage(source, Path.Combine(Path.GetFullPath(packDirectory), "assets"));
        return "assets/" + Path.GetFileName(path);
    }

    public static SharedArtInfo Publish(string originalName, string sourceFileName, byte[] png) =>
        new(SharedArtLibrary.Publish(originalName, sourceFileName, new[] { png }));

    private static SharedArtLibrary.Entry Find(string id) => SharedArtLibrary.ReadEntries()
        .FirstOrDefault(e => string.Equals(e.id, id, StringComparison.Ordinal)) ??
        throw new FileNotFoundException("Shared art entry is unavailable: " + id);
}

public sealed class SharedArtInfo
{
    public string Id { get; }
    public string OriginalName { get; }
    public string SourceFileName { get; }
    public int ViewCount { get; }
    public float PixelsPerUnit { get; }
    internal SharedArtInfo(SharedArtLibrary.Entry entry)
    {
        Id = entry.id; OriginalName = entry.originalName; SourceFileName = entry.sourceFileName;
        ViewCount = entry.views.Length; PixelsPerUnit = entry.pixelsPerUnit;
    }
}
