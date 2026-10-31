#nullable enable
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Silverpine.ModdingTools;

// Plain files, no plugin-specific IDs or runtime dependency. Other mods may
// publish/read the same versioned entries and content-addressed image blobs.
public static class SharedArtLibrary
{
    public static string Root { get; internal set; } = "";
    public sealed class Entry
    {
        public int version = 1;
        public string id = "";
        public string originalName = "";
        public string sourceFileName = "";
        public string? modelSha256;
        public string[] views = Array.Empty<string>(); // Front, Right, Back, Left; or Front only.
        public float pixelsPerUnit = 32;
    }

    public static IEnumerable<Entry> ReadEntries(Action<string>? warning = null)
    {
        string folder = Path.Combine(Root, "entries");
        if (!Directory.Exists(folder)) yield break;
        foreach (string path in Directory.GetFiles(folder, "*.json").OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            Entry? entry = null;
            try
            {
                entry = JsonConvert.DeserializeObject<Entry>(File.ReadAllText(path));
                if (entry == null || entry.version != 1 || !ValidId(entry.id) ||
                    (entry.views.Length != 1 && entry.views.Length != 4) || entry.pixelsPerUnit != 32)
                    throw new InvalidDataException("Unsupported art entry schema.");
                foreach (string view in entry.views)
                    if (!File.Exists(Resolve(view))) throw new FileNotFoundException("Missing cached art", view);
            }
            catch (Exception error) { warning?.Invoke(Path.GetFileName(path) + ": " + error.Message); entry = null; }
            if (entry != null) yield return entry;
        }
    }

    public static string Resolve(string relative)
    {
        if (string.IsNullOrWhiteSpace(Root)) throw new InvalidOperationException("Modding Tools has not initialized the shared art library.");
        if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative))
            throw new InvalidDataException("Art paths must be relative to the shared library.");
        string root = Path.GetFullPath(Root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string path = Path.GetFullPath(Path.Combine(root, relative));
        if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Art path leaves the shared library.");
        return path;
    }

    public static Entry Publish(string originalName, string sourceName, IReadOnlyList<byte[]> pngs, string? modelHash = null)
    {
        if (pngs.Count != 1 && pngs.Count != 4) throw new InvalidDataException("Publish Front or all four directional views.");
        var views = new string[pngs.Count];
        for (int i = 0; i < pngs.Count; i++)
        {
            byte[] bytes = pngs[i];
            if (bytes.Length < 8 || bytes[0] != 137 || bytes[1] != 80 || bytes[2] != 78 || bytes[3] != 71)
                throw new InvalidDataException("Shared art must contain PNG images.");
            views[i] = "images/" + Hash(bytes) + ".png";
        }
        string id = Hash(Encoding.UTF8.GetBytes("art-v1|32|" + string.Join("|", views)));
        var entry = new Entry { id = id, originalName = originalName, sourceFileName = sourceName,
            modelSha256 = modelHash, views = views };
        for (int i = 0; i < views.Length; i++) WriteImmutable(Resolve(views[i]), pngs[i]);
        string manifest = Path.Combine(Root, "entries", id + ".json");
        // The first publisher's original name survives reuse by differently named items.
        if (!File.Exists(manifest)) WriteImmutable(manifest, Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(entry, Formatting.Indented)));
        return JsonConvert.DeserializeObject<Entry>(File.ReadAllText(manifest))!;
    }

    public static string[] CopyToPack(Entry entry, string packFolder)
    {
        if (entry.version != 1 || (entry.views.Length != 1 && entry.views.Length != 4))
            throw new InvalidDataException("Unsupported art entry.");
        return entry.views.Select(view =>
        {
            string source = Resolve(view);
            string target = SharedArtFiles.ImportImage(source, Path.Combine(packFolder, "assets"));
            return "assets/" + Path.GetFileName(target);
        }).ToArray();
    }

    public static string OriginalModelName(string? path)
    {
        if (string.IsNullOrEmpty(path)) return "Cached art";
        string fallback = Path.GetFileNameWithoutExtension(path);
        if (!File.Exists(path)) return fallback;
        try
        {
            using var reader = new BinaryReader(File.OpenRead(path));
            if (reader.ReadUInt32() != 0x46546C67 || reader.ReadUInt32() != 2) return fallback;
            reader.ReadUInt32();
            uint length = reader.ReadUInt32(), type = reader.ReadUInt32();
            if (type != 0x4E4F534A || length > 16 * 1024 * 1024) return fallback;
            JObject json = JObject.Parse(Encoding.UTF8.GetString(reader.ReadBytes((int)length)));
            var names = (json["nodes"] as JArray)?.OfType<JObject>()
                .Select(node => (string?)node["name"]).Where(name => !string.IsNullOrWhiteSpace(name)).ToArray();
            return names?.FirstOrDefault(name => !string.Equals(name, "Scene", StringComparison.OrdinalIgnoreCase)) ?? fallback;
        }
        catch { return fallback; }
    }
    public static string FileHash(string path)
    {
        using var sha = SHA256.Create(); using var input = File.OpenRead(path);
        return Hex(sha.ComputeHash(input));
    }
    private static bool ValidId(string value) => value.Length == 64 && value.All(c => c >= '0' && c <= '9' || c >= 'a' && c <= 'f');
    private static string Hash(byte[] bytes) { using var sha = SHA256.Create(); return Hex(sha.ComputeHash(bytes)); }
    private static string Hex(byte[] bytes) => BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
    private static void WriteImmutable(string path, byte[] bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if (File.Exists(path))
        {
            if (!File.ReadAllBytes(path).SequenceEqual(bytes)) throw new IOException("Art content collision: " + path);
            return;
        }
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllBytes(temporary, bytes);
            try { File.Move(temporary, path); }
            catch (IOException) when (File.Exists(path) && File.ReadAllBytes(path).SequenceEqual(bytes)) { }
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
