param()
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$version=(Get-Content (Join-Path $root 'VERSION') -Raw).Trim()
$stage=Join-Path $root "dist/CyberTrap-Melty-v$version-two-game"
if(Test-Path -LiteralPath $stage){throw 'Use a fresh staging directory; preserve previous release output.'}
New-Item -ItemType Directory -Path $stage | Out-Null
function Copy-Tree([string]$source,[string]$destination){
 New-Item -ItemType Directory -Path $destination -Force | Out-Null
 Get-ChildItem -LiteralPath $source -Force | ForEach-Object {Copy-Item -LiteralPath $_.FullName -Destination $destination -Recurse -Force}
}
$cp=Join-Path $stage 'cyberpunk';$si=Join-Path $stage 'schedule-i'
Copy-Tree (Join-Path $root '.deps/baseline-cp/bin') (Join-Path $cp 'bin')
Copy-Tree (Join-Path $root '.deps/baseline-cp/r6/scripts/StreetChem') (Join-Path $cp 'r6/scripts/StreetChem')
Copy-Tree (Join-Path $root '.deps/baseline-cp/red4ext/plugins/StreetChem') (Join-Path $cp 'red4ext/plugins/StreetChem')
Copy-Tree (Join-Path $root '.deps/baseline-si/Mods') (Join-Path $si 'Mods')
Copy-Item (Join-Path $root 'guest/bin/Release/net6.0/StreetChem.Guest.dll') (Join-Path $si 'Mods/StreetChem.Guest.dll') -Force
Copy-Item (Join-Path $root 'native/build/StreetChemHost.dll') (Join-Path $cp 'red4ext/plugins/StreetChem/StreetChemHost.dll') -Force
Copy-Item (Join-Path $root '.deps/ReShade.fxh') (Join-Path $cp 'bin/x64/streetchem/shaders/ReShade.fxh')
Copy-Tree (Join-Path $root 'launcher/publish') (Join-Path $cp 'CyberTrap/Launcher')
Get-ChildItem (Join-Path $cp 'CyberTrap/Launcher') -Filter '*.pdb' | Remove-Item
$deps=Join-Path $cp 'CyberTrap/Dependencies'
Copy-Tree (Join-Path $root '.deps/codeware') (Join-Path $deps 'Codeware')
Copy-Tree (Join-Path $root '.deps/melon') (Join-Path $deps 'MelonLoader')
Copy-Tree (Join-Path $root '.deps/runtime6') (Join-Path $deps 'Runtime6')
New-Item -ItemType Directory -Path (Join-Path $deps 'ReShade') | Out-Null
Copy-Item (Join-Path $root '.deps/reshade/ReShade64.dll') (Join-Path $deps 'ReShade/dxgi.dll')
$docs=Join-Path $cp 'CyberTrap/Docs';New-Item -ItemType Directory -Path $docs | Out-Null
foreach($name in @('README.md','LICENSE','CREDITS.md','THIRD_PARTY_NOTICES.md','CHANGELOG.md','VERSION','melty.json')){Copy-Item (Join-Path $root $name) (Join-Path $docs $name)}
Copy-Tree (Join-Path $root 'docs') (Join-Path $docs 'docs')
$licenses=Join-Path $docs 'licenses';New-Item -ItemType Directory -Path $licenses | Out-Null
Copy-Item (Join-Path $root '.deps/Codeware-LICENSE') (Join-Path $licenses 'Codeware.txt')
Copy-Item (Join-Path $root '.deps/BepInEx-LICENSE') (Join-Path $licenses 'BepInEx.txt')
Copy-Item (Join-Path $root '.deps/reshade-src/LICENSE.md') (Join-Path $licenses 'ReShade.txt')
# Dependency directories retain their distributed notices (MelonLoader Apache-2.0,
# Microsoft's runtime notices, BepInEx and the unmodified MIT ModKit).
$entries=@(Get-ChildItem -LiteralPath $stage -Recurse -File | ForEach-Object {
 $relative=[IO.Path]::GetRelativePath($stage,$_.FullName).Replace('\','/')
 if($relative -match '(^|/)(Il2CppAssemblies|interop|cache|Saves|saves|Backups)/' -or $_.Extension -in @('.sav','.token','.archive','.assets','.bundle')){throw "Private/game data in payload: $relative"}
 @{path=$relative;size=$_.Length}
})
$entries | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $root 'dist/entries.json') -Encoding utf8NoBOM
$zip=Join-Path $root "dist/CyberTrap-Melty-v$version.zip"
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::CreateFromDirectory($stage,$zip,[IO.Compression.CompressionLevel]::Optimal,$false)
@{fileName=[IO.Path]::GetFileName($zip);size=(Get-Item $zip).Length;sha256=(Get-FileHash $zip -Algorithm SHA256).Hash.ToLowerInvariant()} | ConvertTo-Json | Set-Content (Join-Path $root 'dist/upload.json') -Encoding utf8NoBOM
Get-Content (Join-Path $root 'dist/upload.json')
