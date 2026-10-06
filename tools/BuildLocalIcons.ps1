param(
 [Parameter(Mandatory)][string]$WolvenKitDir,
 [string]$SourcePath=(Join-Path $env:LOCALAPPDATA 'StreetChem\icon-source'),
 [string]$CyberpunkPath,
 [switch]$Install
)
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$wk=(Resolve-Path -LiteralPath $WolvenKitDir).Path
$cli=Join-Path $wk 'WolvenKit.CLI.exe'
if(!(Test-Path -LiteralPath $cli)){throw 'WolvenKit Console is required. Use its official download.'}
if($CyberpunkPath){
 $compression=Join-Path (Resolve-Path -LiteralPath $CyberpunkPath).Path 'bin\x64\oo2ext_7_win64.dll'
 $compressionCache=Join-Path $env:LOCALAPPDATA 'oo2ext_7_win64.dll'
 # WolvenKit's documented loader cache: use the player's own game dependency, never distribute it.
 if(!(Test-Path -LiteralPath $compressionCache) -and (Test-Path -LiteralPath $compression)){Copy-Item -LiteralPath $compression -Destination $compressionCache}
}
if(!(Test-Path -LiteralPath $SourcePath)){throw 'Load the solo Schedule I save with Street Chem v0.2.0 and run the bridge observe command first so product icons are exported from your own game.'}
$source=(Resolve-Path -LiteralPath $SourcePath).Path
$images=@(Get-ChildItem -LiteralPath $source -Filter '*.png' | Where-Object {$_.BaseName -match '^[0-9a-f]{24}$'})
if(!$images.Count){throw 'No exported product icons found'}
$stage=Join-Path $root ('.deps\local-icons-'+[guid]::NewGuid().ToString('N'))
$raw=Join-Path $stage 'raw';$cooked=Join-Path $stage 'streetchem_local_icons';$output=Join-Path $stage 'output'
New-Item -ItemType Directory -Path (Join-Path $raw 'streetchem\icons'),$cooked,$output -Force|Out-Null
foreach($image in $images){Copy-Item -LiteralPath $image.FullName -Destination (Join-Path $raw ('streetchem\icons\'+$image.Name))}
$importMessages=@(& $cli import $raw -o $cooked)
$importExit=$LASTEXITCODE
$importMessages|Write-Output
$imported=@(Get-ChildItem -LiteralPath $cooked -Filter '*.xbm' -Recurse)
if($imported.Count -ne $images.Count -or (($importMessages -join "`n") -notmatch ("Imported "+$images.Count+"/"+$images.Count+" file"))){throw "Incomplete WolvenKit import (code $importExit)"}
foreach($texture in $imported){
 $bytes=[IO.File]::ReadAllBytes($texture.FullName)
 if($bytes.Length -lt 1024 -or [BitConverter]::ToUInt32($bytes,0) -ne 0x57325243){throw "Invalid cooked texture: $($texture.Name)"}
}
$converter=Join-Path $PSScriptRoot 'IconCooker\IconCooker.dll'
if(!(Test-Path -LiteralPath $converter)){
 dotnet build (Join-Path $PSScriptRoot 'IconCooker') -c Release "-p:WolvenKitDir=$wk" -v:minimal
 if($LASTEXITCODE -ne 0){throw 'Atlas converter build failed'}
 $converter=Join-Path $PSScriptRoot 'IconCooker\bin\Release\net10.0\IconCooker.dll'
}
dotnet $converter $wk $cooked
if($LASTEXITCODE -ne 0){throw 'Atlas conversion failed'}
$textures=@(Get-ChildItem -LiteralPath $cooked -Filter '*.xbm' -Recurse)
$atlases=@(Get-ChildItem -LiteralPath $cooked -Filter '*.inkatlas' -Recurse)
if($textures.Count -ne $images.Count -or $atlases.Count -ne $images.Count){throw 'Cooked icon count mismatch'}
& $cli pack $cooked -o $output
$packExit=$LASTEXITCODE
$archive=Join-Path $output 'streetchem_local_icons.archive'
if(!(Test-Path -LiteralPath $archive) -or (Get-Item -LiteralPath $archive).Length -lt 1024){throw "Local archive missing or invalid (code $packExit)"}
if($Install){
 if(!$CyberpunkPath){throw 'Pass CyberpunkPath with -Install'}
 if(Get-Process Cyberpunk2077 -ErrorAction SilentlyContinue){throw 'Close Cyberpunk before installing local icons. Schedule I may stay running.'}
 $cp=(Resolve-Path -LiteralPath $CyberpunkPath).Path
 if(!(Test-Path -LiteralPath (Join-Path $cp 'bin\x64\Cyberpunk2077.exe'))){throw 'Cyberpunk installation not found'}
 $target=Join-Path $cp 'archive\pc\mod\streetchem_local_icons.archive'
 New-Item -ItemType Directory -Path (Split-Path $target) -Force|Out-Null
 if(Test-Path -LiteralPath $target){Copy-Item -LiteralPath $target -Destination (Join-Path $stage 'previous-local-icons.archive')}
 Copy-Item -LiteralPath $archive -Destination $target -Force
 Write-Output "Installed locally generated icon archive: $target"
}
Write-Output "Cooked $($images.Count) local product icons: $archive"
Write-Output 'This contains assets from your own Schedule I installation. Do not upload it to Git or distribute it.'
