using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CyberTrap;

internal sealed class Setup(string root, string backup)
{
    internal readonly string Root = Path.GetFullPath(root);
    readonly string backupRoot = backup;
    readonly List<object> changes = [];

    internal void Write(string path, string value)
    {
        if (File.Exists(path) && File.ReadAllText(path) == value) return;
        Backup(path);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".cybertrap.tmp";
        File.WriteAllText(temp, value, new UTF8Encoding(false));
        File.Move(temp, path, true);
    }

    internal void Backup(string path)
    {
        Directory.CreateDirectory(backupRoot);
        string? saved = null;
        if (File.Exists(path))
        {
            saved = Path.Combine(backupRoot, changes.Count + ".bak");
            File.Copy(path, saved);
        }
        changes.Add(new { path, saved });
        File.WriteAllText(Path.Combine(backupRoot, "restore.json"), JsonSerializer.Serialize(changes));
    }
    internal void Disable(string path)
    {
        if (!File.Exists(path)) return;
        Backup(path);
        // Keep the original adjacent, with a non-loadable extension. Never
        // overwrite another backup or remove files belonging to other mods.
        string parked = path + ".cybertrap-disabled";
        if (File.Exists(parked)) parked += "." + Guid.NewGuid().ToString("N");
        File.Move(path, parked);
    }

    // Frameworks are staged under our own folder, so Melty never overwrites an
    // unrelated proxy DLL. Existing installations are reused, not downgraded.
    internal void InstallMissing(string source, string destination, string marker, string? proxyProduct = null)
    {
        if (File.Exists(Path.Combine(destination, marker))) return;
        foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(destination, Path.GetRelativePath(source, file));
            if (!File.Exists(target)) continue;
            if (SHA256.HashData(File.ReadAllBytes(file)).SequenceEqual(SHA256.HashData(File.ReadAllBytes(target)))) continue;
            // A partly installed framework must not become a mixture of versions.
            throw new InvalidOperationException($"A different or incomplete {proxyProduct ?? "framework"} installation already uses {Path.GetFileName(target)}. Your files were left in place; see the startup log.");
        }
        foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(destination, Path.GetRelativePath(source, file));
            if (File.Exists(target)) continue;
            Backup(target);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
    }

    internal void Ini(string path, string section, string key, string value, bool append = false)
    {
        var text = File.Exists(path) ? File.ReadAllText(path) : "";
        Write(path, SetIni(text, section, key, value, append));
    }

    internal static string SetIni(string text, string section, string key, string value, bool append = false)
    {
        // Edit only this section; other sections may use exactly the same key.
        var lines = text.Replace("\r\n", "\n").Split('\n').ToList();
        int start = lines.FindIndex(x => x.Trim().Equals("[" + section + "]", StringComparison.OrdinalIgnoreCase));
        if (start < 0) { lines.Add("[" + section + "]"); start = lines.Count - 1; }
        int end = start + 1;
        while (end < lines.Count && !lines[end].TrimStart().StartsWith('[')) end++;
        int index = lines.FindIndex(start + 1, end - start - 1,
            x => Regex.IsMatch(x, @"^\s*" + Regex.Escape(key) + @"\s*="));
        if (index >= 0)
        {
            string old = lines[index][(lines[index].IndexOf('=') + 1)..].Trim();
            if (append && old.Split(',').Any(x => x.Trim().Equals(value, StringComparison.OrdinalIgnoreCase))) return text;
            lines[index] = key + "=" + (append && old.Length > 0 ? old.TrimEnd(',') + "," : "") + value;
        }
        else lines.Insert(end, key + "=" + value);
        return string.Join(Environment.NewLine, lines);
    }
}
