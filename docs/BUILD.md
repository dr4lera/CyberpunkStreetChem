# Build from source

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

Run source validation with `tools/Validate.ps1`; perform actual in-game checks before publishing a new supported version. A successful compile is not proof of gameplay behavior.
