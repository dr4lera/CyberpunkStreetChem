# Build from source

v0.2.0 also includes the optional local icon converter. Install the .NET 10 SDK and supply an official WolvenKit Console folder to build it before packaging:

```powershell
dotnet build .\tools\IconCooker -c Release -p:WolvenKitDir='D:\Tools\WolvenKit.Console'
```

The standalone converter uses GPL-3.0-only and the user's unmodified WolvenKit libraries. Its own source/license accompany its binary. No WolvenKit assemblies, native game compression DLLs, generated PNGs, `.xbm` textures or local `.archive` files enter the public payload. Follow [CONSUMABLES.md](CONSUMABLES.md) to cook the player's own product icons locally.

End users should install the release ZIP. Source builds require Windows x64, Visual Studio C++ Build Tools, a .NET SDK capable of targeting .NET 6, and the user's MelonLoader-generated Schedule I IL2CPP assemblies. No retail assemblies are stored in this repository.

Download the **ReShade SDK**, **RED4ext.SDK**, and nlohmann's single-header `json.hpp` from the official projects credited in CREDITS.md. Keep them outside tracked source. Supply their paths:

```powershell
pwsh -NoProfile -File .\tools\Build.ps1 `
  -ScheduleIPath 'D:\Games\Schedule I' `
  -MelonLoaderDir 'D:\Games\Schedule I\MelonLoader' `
  -RED4ExtSDKDir 'D:\SDKs\RED4ext.SDK' `
  -ReShadeSDKDir 'D:\SDKs\ReShadeSDK' `
  -JsonIncludeDir 'D:\SDKs\json'
pwsh -NoProfile -File .\tools\Package.ps1
```

`JsonIncludeDir` must contain `json.hpp`; ReShadeSDKDir must contain `reshade.hpp`. RED4ext.SDK's vendor dependencies must be present. The generated constants are committed so Python is not required to build. Binaries, dependency folders and package output are ignored by Git. Release packaging uses an explicit payload list and excludes prototype multiplayer, saves, credentials and retail content.

Packaging creates the combined installer ZIP plus separate Cyberpunk and Schedule I ZIPs rooted at their respective game directories. `tools/Package.ps1 -SplitOnly` creates just the two game archives when the combined package already exists. Existing output stages must be moved aside before repackaging.

Run source validation with `tools/Validate.ps1`; perform actual in-game checks before publishing a new supported version. A successful compile is not proof of gameplay behavior.

Dealer scripts also require Codeware 1.20.5 when compiling locally. `GuestShop.cs` exports native offers; `GuestSales.cs` commits purchases; `StreetChemDealer.reds` owns NPC spawning and shopping. Install Codeware separately. Release payload: eight Cyberpunk files, one Schedule I DLL.

## Nivalis source and three-game packages

`nivalis/guest` targets .NET 6 with BepInEx IL2CPP interfaces from your owned Nivalis installation. Supply `-p:NivalisPath=<game-root>` and `-p:NivalisModKitPath=<locally built ModKit.dll>`. Build ModKit from upstream commit 65ce1e01afdb1d56f171761ff1ff619a4ec148ec; its MIT license must accompany its distributed DLL.

`nivalis/native/build.cmd` uses `NC_RED4EXT_SDK_DIR` and `NC_JSON_INCLUDE_DIR`. Compile both script folders with redscript, Codeware and TweakXL interfaces. Do not publish retail/generated game assemblies.

`tools/PackageCyberTrap.ps1 -ModKitPath <built DLL>` creates the current three normal game-root ZIPs and checksums. The all-in-one launcher/framework installer is developed separately and is not packaged here.
