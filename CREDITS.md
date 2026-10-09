# Credits

- Concept, testing and project owner: zrock.
- Implementation and documentation: zrock with OpenAI Codex (GPT-6), AI assisted.
- Cyberpunk 2077: CD PROJEKT RED; Schedule I: TVGS. Players supply their own installed content. No retail assets are distributed here.
- RED4ext and RED4ext.SDK: https://github.com/WopsS/RED4ext and https://github.com/WopsS/RED4ext.SDK . Header-only SDK portions retain their project licensing.
- redscript: https://github.com/jac3km4/redscript .
- MelonLoader: https://github.com/LavaGang/MelonLoader .
- Harmony and Il2CppInterop are used through the user's MelonLoader installation.
- ReShade and its SDK: https://reshade.me and https://github.com/crosire/reshade . Embedded SDK headers use the MIT licensing option; see THIRD_PARTY_NOTICES.md.
- nlohmann/json: https://github.com/nlohmann/json (MIT); used in the native host binary.
- WolvenKit Console / RED4 types: https://github.com/WolvenKit/WolvenKit (GPLv3). The standalone optional IconCooker is GPL-3.0-only, with its source and license in `tools/IconCooker`; the player supplies WolvenKit. Core mod code remains MIT.

This release includes no generated art/audio or fal assets. No affiliation with either game's publisher or Melty is claimed. Dependency runtimes and shaders are downloaded from their official projects, not redistributed in this ZIP. Third-party header licenses included in THIRD_PARTY_NOTICES.md apply to embedded SDK/library portions.

- [Codeware](https://github.com/psiberx/cp2077-codeware), by psiberx, provides persistent NPC spawning. Installed separately.
- [Appearance Menu Mod](https://github.com/MaximiliumM/appearancemenumod), by MaximiliumM and contributors, supplied references for H10 coordinates and the vanilla drug-dealer record. No AMM code or database is bundled.

## Nivalis integration

BGASM: Nivalis ModKit 0.6.1 (MIT), built from upstream commit 65ce1e01afdb1d56f171761ff1ff619a4ec148ec. BepInEx/Il2CppInterop authors provide the Nivalis loading/runtime route. Codeware/TweakXL provide native Cyberpunk spawning and records. Nivalis, Cyberpunk and Schedule I retail content belongs to their creators and is not included. Original bridge and manager code was implemented with Codex assistance for dr4lera.
