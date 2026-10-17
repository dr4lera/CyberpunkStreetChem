using System.Diagnostics;
using System.IO.Pipes;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace CyberTrap;

internal static class Program
{
    static readonly string LogDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CyberTrap", "logs");
    static string log = "";
    static void Note(string text) { Directory.CreateDirectory(LogDir); File.AppendAllText(log, $"{DateTimeOffset.Now:O} {text}\n"); }
    [STAThread]
    static int Main(string[] args)
    {
        if (args.Contains("--self-test")) return SelfTest.Run();
        log = Path.Combine(LogDir, "startup-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + ".log");
        try { return Run(args).GetAwaiter().GetResult(); }
        catch (Exception e)
        {
            Note(e.ToString());
            if (!args.Contains("--verify-only")) MessageBox.Show(e.Message + "\n\nYour games and saves have been left open and in place. Startup details were saved for diagnosis.", "CyberTrap could not start", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return 1;
        }
    }

    static string Arg(string[] args, string key)
    {
        int i = Array.IndexOf(args, key);
        if (i < 0 || i + 1 >= args.Length || args[i + 1].StartsWith("--")) throw new ArgumentException("Melty did not supply the required " + key[2..] + " game folder. Press Play from the updated listing.");
        return Path.GetFullPath(args[i + 1]);
    }
    static void Need(string path) { if (!File.Exists(path)) throw new FileNotFoundException("A required game or release file is missing: " + Path.GetFileName(path), path); }
    static Process? Existing(string exe)
    {
        foreach (var process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(exe)))
        {
            try { if (Path.GetFullPath(process.MainModule!.FileName).Equals(exe, StringComparison.OrdinalIgnoreCase)) return process; }
            catch (System.ComponentModel.Win32Exception) { throw new InvalidOperationException("A required game is already running with different Windows permissions. Close that game normally before pressing Play."); }
            process.Dispose();
        }
        return null;
    }
    static Process Start(string exe, string? runtime = null)
    {
        var info = new ProcessStartInfo(exe) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(exe)! };
        if (runtime != null) { info.Environment["DOTNET_ROOT"] = runtime; info.Environment["DOTNET_ROOT_X64"] = runtime; }
        // Do not set resolution, window position, visibility or graphics options.
        var process = Process.Start(info) ?? throw new InvalidOperationException("Windows did not start " + Path.GetFileName(exe));
        Note($"Started {Path.GetFileName(exe)} pid={process.Id}");
        return process;
    }

    static async Task<int> Run(string[] args)
    {
        string cp = Arg(args, "--cyberpunk"), si = Arg(args, "--schedule");
        string cpExe = Path.Combine(cp, "bin/x64/Cyberpunk2077.exe"), siExe = Path.Combine(si, "Schedule I.exe");
        foreach (string exe in new[] { cpExe, siExe }) Need(exe);
        if (!File.Exists(Path.Combine(si, "GameAssembly.dll"))) throw new InvalidOperationException("This release needs Schedule I's IL2CPP branch. The Mono branch cannot load this bridge.");
        string files = Path.Combine(cp, "CyberTrap"), deps = Path.Combine(files, "Dependencies");
        foreach (var path in new[] { Path.Combine(cp, "red4ext/RED4ext.dll"), Path.Combine(cp, "engine/tools/scc.exe"), Path.Combine(cp, "red4ext/plugins/TweakXL/TweakXL.dll"), Path.Combine(si, "Mods/StreetChem.Guest.dll") }) Need(path);
        if (args.Contains("--verify-only")) { Note("Release paths verified; no games started or files changed."); return 0; }
        Directory.CreateDirectory(LogDir);
        using var sessionLock = new FileStream(Path.Combine(LogDir, "session.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        try
        {
            // Never reconfigure or attach to the creator's already running games.
            foreach (string exe in new[] { cpExe, siExe })
                using (var existing = Existing(exe)) if (existing != null) throw new InvalidOperationException(Path.GetFileNameWithoutExtension(exe) + " is already open. Close it normally before starting this CyberTrap session.");
            var setup = new Setup(cp, Path.Combine(files, "Backups", DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")));
            setup.InstallMissing(Path.Combine(deps, "Codeware"), cp, "red4ext/plugins/Codeware/Codeware.dll", "Codeware");
            setup.InstallMissing(Path.Combine(deps, "MelonLoader"), si, "MelonLoader/net6/MelonLoader.dll", "MelonLoader");
            // This release intentionally pauses Nivalis. Preserve, but disable,
            // only our old host files so upgrades do not keep running that half.
            foreach (string relative in new[] { "red4ext/plugins/NivalisNightCity/NivalisNightCityHost.dll", "r6/scripts/NivalisNightCity/BusinessManagers.reds", "r6/scripts/NivalisNightCity/BusinessSites.reds", "r6/scripts/NivalisNightCity/NivalisNightCity.reds" })
                setup.Disable(Path.Combine(cp, relative));
            string bin = Path.GetDirectoryName(cpExe)!;
            string proxy = Path.Combine(bin, "dxgi.dll");
            if (File.Exists(proxy))
            {
                var info = FileVersionInfo.GetVersionInfo(proxy);
                if (!(info.ProductName ?? "").Contains("ReShade", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Another graphics tool already uses Cyberpunk's DXGI file. It was left in place; CyberTrap cannot install its renderer safely over it.");
                // Standard ReShade cannot load our add-on; upgrade a known ReShade
                // proxy to the tested full-add-on build, keeping the old file.
                if (!System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(proxy)).SequenceEqual(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(Path.Combine(deps, "ReShade/dxgi.dll")))))
                { setup.Backup(proxy); File.Copy(Path.Combine(deps, "ReShade/dxgi.dll"), proxy, true); }
            }
            else setup.InstallMissing(Path.Combine(deps, "ReShade"), bin, "dxgi.dll", "ReShade");
            string ini = Path.Combine(bin, "ReShade.ini");
            setup.Ini(ini, "GENERAL", "EffectSearchPaths", @".\streetchem\shaders", true);
            string preset = ReadIni(ini, "GENERAL", "PresetPath") ?? @".\streetchem\StreetChemPreset.ini";
            if (string.IsNullOrWhiteSpace(preset)) preset = @".\streetchem\StreetChemPreset.ini";
            setup.Ini(ini, "GENERAL", "PresetPath", preset);
            if (!Path.IsPathRooted(preset)) preset = Path.GetFullPath(Path.Combine(bin, preset));
            // ReShade preset techniques are deliberately outside a section.
            var presetText = File.Exists(preset) ? File.ReadAllText(preset) : "";
            foreach (var key in new[] { "Techniques", "TechniqueSorting" }) presetText = PresetKey(presetText, key, "StreetChemLab@StreetChemLab.fx");
            setup.Write(preset, presetText);
            Note("Setup complete. Existing graphics and window settings preserved.");
            using var schedule = Start(siExe, Path.Combine(deps, "Runtime6"));
            Note("Waiting for Schedule I's native bridge; first-time interface generation may take several minutes.");
            await WaitFor(schedule, "Schedule I", ScheduleReady, TimeSpan.FromMinutes(10));
            // The native Continue path selects the player's own last solo save.
            // With no previous Schedule I save its normal new-game menu stays up.
            await ScheduleContinue();
            // Direct executable and correct working folder: store launchers can
            // exit before the actual game, misleading Play's lifetime tracking.
            using var host = Start(cpExe);
            Note("Schedule I bridge ready. Cyberpunk started; launcher follows its actual lifetime without a time limit.");
            await host.WaitForExitAsync();
            Note("Cyberpunk exited, code=" + host.ExitCode + ". Companion games were not force-closed.");
            return host.ExitCode;
        }
        finally { Note("Session tracking ended; no processes were killed."); }
    }

    static async Task WaitFor(Process game, string name, Func<Task<bool>> ready, TimeSpan timeout)
    {
        var timer = Stopwatch.StartNew();
        while (timer.Elapsed < timeout)
        {
            if (game.HasExited) throw new InvalidOperationException(name + " closed during startup (exit " + game.ExitCode + "). Cyberpunk was not started. The game's loader log has the cause.");
            if (await ready()) { Note(name + " bridge is ready."); return; }
            await Task.Delay(1000);
        }
        throw new TimeoutException(name + " is still preparing its bridge. It was left running so setup can finish. Close it normally and press Play again when ready.");
    }
    static Task<bool> ScheduleReady() => ScheduleReady("StreetChem.Control.v1");
    internal static async Task<bool> ScheduleReady(string pipeName)
    {
        try
        {
            using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            using var cts = new CancellationTokenSource(2000);
            await pipe.ConnectAsync(cts.Token);
            using var writer = new StreamWriter(pipe, new UTF8Encoding(false), 4096, true) { AutoFlush = true };
            using var reader = new StreamReader(pipe, Encoding.UTF8, false, 4096, true);
            await writer.WriteLineAsync("{\"op\":\"observe\"}");
            var line = await reader.ReadLineAsync(cts.Token);
            using var json = JsonDocument.Parse(line ?? "{}");
            return json.RootElement.TryGetProperty("ok", out var ok) && ok.GetBoolean();
        }
        catch (Exception e) when (e is IOException or OperationCanceledException or JsonException) { return false; }
    }
    static async Task<bool> BusinessReady(string nv)
    {
        try
        {
            var token = File.ReadAllText(Path.Combine(nv, "BepInEx/cache/nivalismodkit-bridge.token")).Trim();
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            using var request = new HttpRequestMessage(HttpMethod.Post, "http://127.0.0.1:5710/cmd/nc-bootstrap-status");
            request.Headers.Add("X-Kit-Token", token);
            using var response = await client.SendAsync(request);
            return response.IsSuccessStatusCode;
        }
        catch (Exception e) when (e is IOException or HttpRequestException or OperationCanceledException) { return false; }
    }
    internal static bool DedicatedSaveReady(JsonElement status, string? expected) =>
        status.TryGetProperty("ready", out var ready) && ready.ValueKind == JsonValueKind.True &&
        !string.IsNullOrWhiteSpace(expected) && status.TryGetProperty("save", out var save) && save.GetString() == expected;
    static async Task<bool> BusinessStart(string nv)
    {
        try
        {
            string token = File.ReadAllText(Path.Combine(nv, "BepInEx/cache/nivalismodkit-bridge.token")).Trim();
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            using var request = new HttpRequestMessage(HttpMethod.Post, "http://127.0.0.1:5710/cmd/nc-start-melty");
            request.Headers.Add("X-Kit-Token", token);
            using var response = await client.SendAsync(request);
            if (!response.IsSuccessStatusCode) return false;
            using var statusRequest = new HttpRequestMessage(HttpMethod.Post, "http://127.0.0.1:5710/cmd/nc-bootstrap-status");
            statusRequest.Headers.Add("X-Kit-Token", token);
            using var statusResponse = await client.SendAsync(statusRequest);
            if (!statusResponse.IsSuccessStatusCode) return false;
            using var status = JsonDocument.Parse(await statusResponse.Content.ReadAsStringAsync());
            return DedicatedSaveReady(status.RootElement, ReadIni(Path.Combine(nv, "BepInEx/config/dr4lera.nivalis.nightcity.cfg"), "Lab", "SaveName"));
        }
        catch (Exception e) when (e is IOException or HttpRequestException or OperationCanceledException) { return false; }
    }
    static async Task ScheduleContinue()
    {
        using var pipe = new NamedPipeClientStream(".", "StreetChem.Control.v1", PipeDirection.InOut, PipeOptions.Asynchronous);
        using var cts = new CancellationTokenSource(5000);
        await pipe.ConnectAsync(cts.Token);
        using var writer = new StreamWriter(pipe, new UTF8Encoding(false), 4096, true) { AutoFlush = true };
        using var reader = new StreamReader(pipe, Encoding.UTF8, false, 4096, true);
        await writer.WriteLineAsync("{\"op\":\"load_last\"}");
        string? reply = await reader.ReadLineAsync(cts.Token);
        using var json = JsonDocument.Parse(reply ?? "{}");
        Note("Schedule I native Continue: " + (json.RootElement.TryGetProperty("status", out var status) ? status.GetString() : "select or create your native solo save in its menu"));
    }
    internal static string? ReadIni(string path, string section, string key)
    {
        if (!File.Exists(path)) return null;
        bool inside = false;
        foreach (string raw in File.ReadLines(path))
        {
            string line = raw.Trim();
            if (line.StartsWith('[')) inside = line.Equals("[" + section + "]", StringComparison.OrdinalIgnoreCase);
            if (inside && line.StartsWith(key + " ", StringComparison.OrdinalIgnoreCase) || inside && line.StartsWith(key + "=", StringComparison.OrdinalIgnoreCase))
            { int at = line.IndexOf('='); if (at >= 0) return line[(at + 1)..].Trim(); }
        }
        return null;
    }
    internal static string PresetKey(string text, string key, string technique)
    {
        var regex = new System.Text.RegularExpressions.Regex(@"(?m)^" + key + @"=([^\r\n]*)");
        var match = regex.Match(text);
        if (match.Success && match.Groups[1].Value.Split(',').Contains(technique)) return text;
        return match.Success ? regex.Replace(text, _ => key + "=" + match.Groups[1].Value.TrimEnd(',') + "," + technique, 1) : key + "=" + technique + Environment.NewLine + text;
    }
    static void ConfigurePair(Setup setup, string cp, string nv)
    {
        string cfg = Path.Combine(nv, "BepInEx/config/dr4lera.nivalis.nightcity.cfg");
        string? save = ReadIni(cfg, "Lab", "SaveName");
        if (string.IsNullOrWhiteSpace(save))
        {
            // No imported developer save. A dedicated copy is made only from the
            // player's own native save and its matching ModKit sidecar.
            string saves = Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "../LocalLow/ION LANDS/Nivalis Nights"));
            string destination = Path.Combine(saves, "cybertrap_melty.sav");
            if (!File.Exists(destination))
            {
                var source = Directory.Exists(saves) ? Directory.EnumerateFiles(saves, "*.sav").Where(x => !Path.GetFileName(x).Equals("AUTOSAVE.sav", StringComparison.OrdinalIgnoreCase)).OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault() : null;
                if (source != null)
                {
                    File.Copy(source, destination);
                    string sidecar = Path.ChangeExtension(source, ".modkit.json");
                    if (File.Exists(sidecar)) File.Copy(sidecar, Path.ChangeExtension(destination, ".modkit.json"));
                }
            }
            save = "cybertrap_melty";
            setup.Ini(cfg, "Lab", "SaveName", save);
        }
        string path = Path.Combine(cp, "red4ext/plugins/NivalisNightCity/NivalisNightCity.json");
        var data = File.Exists(path) ? JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(File.ReadAllText(path))! : [];
        data["nivalisGame"] = JsonSerializer.SerializeToElement(nv);
        data["save"] = JsonSerializer.SerializeToElement(save);
        if (!data.ContainsKey("autoRestock")) data["autoRestock"] = JsonSerializer.SerializeToElement(true);
        setup.Write(path, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
    }
}
