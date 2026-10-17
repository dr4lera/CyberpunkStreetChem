# CyberTrap Melty startup repair

Listing: d352fdc7-2fd2-4405-bc85-ca04775dee58, slug first-experimental-solo-releas.
Source baseline: ccba92e, public release 0.3.0. Target: 0.3.1.

Design preserved: solo Cyberpunk 2077 host, real Schedule I and Nivalis Nights companions. No substitute games, assets, saves or multiplayer.

Player issue keys: ended:quick-close (10 players, about 45 seconds), ended:never-started (9). No player logs exist; individual causes cannot be proven from these reports.

Confirmed release defects: both companion components optional; recipe declares only Cyberpunk, maps Schedule I Mods into Cyberpunk, never maps Nivalis, starts only Cyberpunk; lacks Codeware/TweakXL/ReShade/MelonLoader/BepInEx setup. README explicitly says launcher not included. Clean players cannot receive the documented mashup. Local machine has all three games and preexisting frameworks, so a baseline pass here cannot establish clean-install compatibility.

Route: keep native game bridges, add self-contained Windows bootstrap launcher and complete required three-game recipe. Bundle licensed dependencies; never bundle retail/generated game assemblies or private configs. Preserve existing windows and files with backups, refuse conflicts, never kill existing games. Track actual host lifetime rather than a store/prelauncher. Test connection checks must come from real loaded plugins and gameplay, never launcher startup alone.

Baseline Melty test: 5ba2f243-9fd3-43dc-9275-f1e75981e783, one 2-minute run of 0.3.0. Await result.

Final main upload: 701fdde6-2442-4db0-925c-b856ea483a90, SHA256 52344267a0b971ad2b7a74df8855d7caf3c09e78365cbe88ebb734b60eecc959, 147099692 bytes. Notices/source upload: 0fdd7381-256a-435d-a760-86aed0f58d93. Recipe validates with 942 mapped files and none unmapped; one-click check says yes. Baseline test did not reproduce a close; failed because ArchiveXL/TweakXL were absent from its recipe and never entered gameplay. First bootstrap test found reader disposal closing the pipe before writer disposal; fixed both readiness and Continue paths and added a real named-pipe regression. Nine setup regression checks pass. Current final test: 41433153-ecfe-4794-a9f3-7915caad200e (2 x 3 minutes). Do not resubmit this draft during its test: that invalidates the test. No existing user games were closed; test-owned processes were closed by Melty.

Final corrected main upload: 71944c90-1c3b-4cae-ba05-5d79d30dbe29, SHA256 6c9113d073528ca45b0114fc62f500f49eea272430b4c551f4690363ea1ac3db, 147100773 bytes. Current test c64a4fdb-5ef6-42a8-88c8-26efff6e8cf1 (2 x 3 min). Native test exposed Schedule I locally updated to 0.4.7f12: TimeManager sleep and skip members moved. Adapted GuestClock through runtime API discovery, preserving the legacy path; each feature tick is isolated so an exception cannot starve the control queue. Guest builds successfully against the player's newly generated 0.4.7 IL2CPP interfaces. No retail interfaces are packaged.

Creator explicitly requested cutting Nivalis for now. Final release scope is Cyberpunk + Schedule I only. Removed Nivalis game requirement, runtime payload, host scripts/plugin and startup from recipe/package. Bootstrap preserves and disables only old Nivalis host files on upgrade; it does not touch or close an existing Nivalis game. Nivalis source retained for future work. Listing and README updated to the agreed two-game scope. Prior Nivalis tests are not used as passing evidence.

Final two-game release: a166ebbc-5b8a-4375-9fab-b187d9352da4, 111030010 bytes, SHA256 d1d59e10ae4e768c1e33bc15f0aab2b0e3f837e6c5a3f434ef79364f50b02068. Current test 8eb9b10a-be72-4b3c-9db1-aef6c08d4aa5 (2 x 3 min), checks mod-loads, guest-connected, host-gameplay. Nivalis removed by explicit creator instruction. 702 main archive entries plus 6 notices entries are mapped; no unmapped files; one-click check yes. Do not resubmit until this test completes.

## 2026-10-17 final 0.3.1 two-game publication
- Creator requested removal of Nivalis to reduce load, then explicitly stopped recording because of lag and asked to publish.
- Release 4937607c-dd79-46fc-8657-8f18047daae5; main upload 76d390d7-cd64-49eb-9876-4bdd43dce298, SHA256 781bf92844e7812e965616427912f83e87e8d1fdc22536a012446a510866f3b9, 111030396 bytes, 702 files. Required notices retained.
- Test 8eb9b10a-be72-4b3c-9db1-aef6c08d4aa5 reached street gameplay and passed all own checks, but loader-error check failed: native SI storage Sum overflow. Final IItemSlotOwner.GetQuantitySum patch uses long and saturates only int aggregate without changing stock. Build and five isolated prefix tests passed; final patch not rerun in game per recording request.
- Updated listing title, two required games, feature scope, RAM guidance, Nivalis removal and testing limits. Real 0.3.1 screenshot approved as cover.
- Submitted and publish requested; currently safety review. Both player issue keys recorded with qualified evidence, not claimed exact per-player reproduction.
