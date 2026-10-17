namespace CyberTrap;

internal static class SelfTest
{
    internal static int Run()
    {
        string dir = Path.Combine(Path.GetTempPath(), "CyberTrap-Setup-Test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        void Assert(bool value, string name) { if (!value) throw new InvalidOperationException(name); }
        try
        {
            const string ini = "[GENERAL]\nPresetPath=mine.ini\nEffectSearchPaths=custom\n[OTHER]\nPresetPath=other.ini\n";
            var edited = Setup.SetIni(ini, "GENERAL", "EffectSearchPaths", @".\streetchem\shaders", true);
            Assert(edited.Contains("EffectSearchPaths=custom,.\\streetchem\\shaders"), "preserve existing effect paths");
            Assert(edited.Contains("PresetPath=other.ini"), "preserve unrelated section");
            Assert(Setup.SetIni(edited, "GENERAL", "EffectSearchPaths", @".\streetchem\shaders", true) == edited, "idempotent ini update");
            string path = Path.Combine(dir, "test.ini"); File.WriteAllText(path, ini);
            Assert(Program.ReadIni(path, "GENERAL", "PresetPath") == "mine.ini", "read correct section");
            string preset = "Techniques=Other@Other.fx\n[Other.fx]\nValue=3\n";
            string merged = Program.PresetKey(preset, "Techniques", "StreetChemLab@StreetChemLab.fx");
            Assert(merged.Contains("Other@Other.fx,StreetChemLab@StreetChemLab.fx"), "keep other techniques");
            Assert(Program.PresetKey(merged, "Techniques", "StreetChemLab@StreetChemLab.fx") == merged, "idempotent preset");
            string source = Path.Combine(dir, "source"), target = Path.Combine(dir, "game");
            Directory.CreateDirectory(source); Directory.CreateDirectory(target);
            File.WriteAllText(Path.Combine(source, "version.dll"), "our framework");
            File.WriteAllText(Path.Combine(target, "version.dll"), "another proxy");
            var setup = new Setup(target, Path.Combine(dir, "backups"));
            bool blocked = false;
            try { setup.InstallMissing(source, target, "loader.dll"); } catch (InvalidOperationException) { blocked = true; }
            Assert(blocked && File.ReadAllText(Path.Combine(target, "version.dll")) == "another proxy", "reject proxy conflict without overwriting");
            setup.Write(path, "changed");
            Assert(File.ReadAllText(Path.Combine(dir, "backups/0.bak")) == ini, "backup original configuration");
            File.WriteAllText(Path.Combine(target, "loader.dll"), "existing loader");
            setup.InstallMissing(source, target, "loader.dll");
            Assert(File.ReadAllText(Path.Combine(target, "version.dll")) == "another proxy", "reuse existing installation");
            string pipeName = "CyberTrap.SetupTest." + Guid.NewGuid().ToString("N");
            var server = Task.Run(async () =>
            {
                using var pipe = new System.IO.Pipes.NamedPipeServerStream(pipeName, System.IO.Pipes.PipeDirection.InOut, 1, System.IO.Pipes.PipeTransmissionMode.Byte, System.IO.Pipes.PipeOptions.Asynchronous);
                await pipe.WaitForConnectionAsync();
                using var reader = new StreamReader(pipe, System.Text.Encoding.UTF8, false, 4096, true);
                using var writer = new StreamWriter(pipe, new System.Text.UTF8Encoding(false), 4096, true) { AutoFlush = true };
                Assert(await reader.ReadLineAsync() == "{\"op\":\"observe\"}", "native readiness request");
                await writer.WriteLineAsync("{\"ok\":true,\"loaded\":false}");
                await Task.Delay(100);
            });
            Assert(Program.ScheduleReady(pipeName).GetAwaiter().GetResult(), "readiness pipe can be disposed after a valid reply");
            server.GetAwaiter().GetResult();
            using var title = System.Text.Json.JsonDocument.Parse("{\"ready\":false,\"save\":null}");
            using var wrongSave = System.Text.Json.JsonDocument.Parse("{\"ready\":true,\"save\":\"normal-save\"}");
            using var dedicated = System.Text.Json.JsonDocument.Parse("{\"ready\":true,\"save\":\"cybertrap_melty\"}");
            Assert(!Program.DedicatedSaveReady(title.RootElement,"cybertrap_melty"), "a title screen is not a loaded business save");
            Assert(!Program.DedicatedSaveReady(wrongSave.RootElement,"cybertrap_melty"), "a normal save is not a dedicated business save");
            Assert(Program.DedicatedSaveReady(dedicated.RootElement,"cybertrap_melty"), "confirmed dedicated save can launch host");
            File.WriteAllText(Path.Combine(dir, "passed.txt"), "12 setup regression checks passed, including native readiness/disposal and dedicated-save readiness; no games started.");
            return 0;
        }
        catch (Exception e) { File.WriteAllText(Path.Combine(dir, "failed.txt"), e.ToString()); return 1; }
    }
}
