#nullable enable
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Silverpine.ModdingTools;

internal static class SharedArtFiles
{
    internal static string ImportImage(string source, string assets)
    {
        byte[] bytes = File.ReadAllBytes(source);
        Directory.CreateDirectory(assets);
        string destination = Path.Combine(assets,
            Hash(bytes) + Path.GetExtension(source).ToLowerInvariant());
        if (File.Exists(destination))
        {
            if (!Equal(bytes, File.ReadAllBytes(destination)))
                throw new IOException("The content-addressed asset already exists with different contents: " + destination);
            return destination;
        }
        // Publish only complete files; interruption must not poison a content-addressed name.
        string temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllBytes(temporary, bytes);
            try { File.Move(temporary, destination); }
            catch (IOException) when (File.Exists(destination) && Equal(bytes, File.ReadAllBytes(destination))) { }
            return destination;
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private static bool Equal(byte[] a, byte[] b)
    {
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
        return true;
    }

    private static string Hash(byte[] bytes)
    {
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
    }
}
