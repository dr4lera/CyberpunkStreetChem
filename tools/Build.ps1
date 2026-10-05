param(
 [Parameter(Mandatory)][string]$ScheduleIPath,
 [Parameter(Mandatory)][string]$MelonLoaderDir,
 [Parameter(Mandatory)][string]$RED4ExtSDKDir,
 [Parameter(Mandatory)][string]$ReShadeSDKDir,
 [Parameter(Mandatory)][string]$JsonIncludeDir
)
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
foreach($path in @((Join-Path $RED4ExtSDKDir 'include\RED4ext\RED4ext.hpp'),(Join-Path $ReShadeSDKDir 'reshade.hpp'),(Join-Path $JsonIncludeDir 'json.hpp'))){if(!(Test-Path -LiteralPath $path)){throw "Missing build dependency: $path"}}
$env:SC_RED4EXT_SDK_DIR=(Resolve-Path -LiteralPath $RED4ExtSDKDir).Path
$env:SC_RESHADE_SDK_DIR=(Resolve-Path -LiteralPath $ReShadeSDKDir).Path
$env:SC_JSON_INCLUDE_DIR=(Resolve-Path -LiteralPath $JsonIncludeDir).Path
& (Join-Path $root 'native\build.cmd')
if($LASTEXITCODE -ne 0){throw 'Native build failed'}
dotnet build (Join-Path $root 'guest') -c Release "-p:ScheduleIPath=$ScheduleIPath" "-p:MelonLoaderDir=$MelonLoaderDir"
if($LASTEXITCODE -ne 0){throw 'Guest build failed'}
Write-Output 'Solo binaries built. Run tools/Package.ps1 to create the install ZIP.'
