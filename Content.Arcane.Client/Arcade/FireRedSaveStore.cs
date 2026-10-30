using System;
using System.IO;
using System.Linq;
using Robust.Shared.ContentPack;
using Robust.Shared.Utility;

namespace Content.Arcane.Client.Arcade;

/// <summary>Crash-tolerant, per-client SRAM storage for the built-in FireRed cabinet.</summary>
internal sealed class FireRedSaveStore(IResourceManager resources)
{
    public const int MaxSaveBytes = 1_048_576;

    private static readonly ResPath Directory = new("/ArcadeSaves/");
    private const string Prefix = "firered-";

    public byte[]? Load()
    {
        if (!resources.UserData.IsDir(Directory))
            return null;

        var candidates = resources.UserData.DirectoryEntries(Directory)
            .Where(IsCommittedSave)
            .OrderByDescending(x => x, StringComparer.Ordinal)
            .ToArray();
        foreach (var name in candidates)
        {
            try
            {
                var data = resources.UserData.ReadAllBytes(Directory / new ResPath(name));
                if (data.Length is > 0 and <= MaxSaveBytes)
                    return data;
            }
            catch (IOException)
            {
                // Try the previous complete generation if the newest file is damaged.
            }
        }

        if (candidates.Length > 0)
            throw new InvalidDataException("No committed FireRed save generation is readable.");
        return null;
    }

    public void Save(byte[] data)
    {
        if (data.Length is 0 or > MaxSaveBytes)
            throw new InvalidDataException("FireRed SRAM payload is outside the allowed size.");

        resources.UserData.CreateDir(Directory);
        var id = DateTime.UtcNow.Ticks.ToString("D19") + "-" + Guid.NewGuid().ToString("N");
        var temporary = Directory / new ResPath(Prefix + id + ".tmp");
        var committed = Directory / new ResPath(Prefix + id + ".sav");

        try
        {
            using (var stream = resources.UserData.Open(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(data);
                stream.Flush();
            }

            // Unique committed names let Rename act as an atomic commit without replacing
            // the last known-good save (the provider does not expose replace semantics).
            resources.UserData.Rename(temporary, committed);
            PruneOldGenerations();
        }
        catch
        {
            try
            {
                resources.UserData.Delete(temporary);
            }
            catch (IOException)
            {
                // Preserve the original write/commit exception.
            }

            throw;
        }
    }

    private static bool IsCommittedSave(string name) =>
        name.StartsWith(Prefix, StringComparison.Ordinal) && name.EndsWith(".sav", StringComparison.Ordinal) &&
        name.Length == Prefix.Length + 19 + 1 + 32 + 4;

    private void PruneOldGenerations()
    {
        foreach (var old in resources.UserData.DirectoryEntries(Directory)
                     .Where(IsCommittedSave)
                     .OrderByDescending(x => x, StringComparer.Ordinal)
                     .Skip(3))
        {
            try
            {
                resources.UserData.Delete(Directory / new ResPath(old));
            }
            catch (IOException)
            {
                // Retaining an old generation is safer than failing a committed save.
            }
        }
    }
}
