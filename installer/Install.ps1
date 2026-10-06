[CmdletBinding(SupportsShouldProcess)]
param(
 [Parameter(Mandatory)][string]$CyberpunkPath,
 [Parameter(Mandatory)][string]$ScheduleIPath,
 [string]$ReShadeIncludePath,
 [string]$BackupRoot=(Join-Path $env:LOCALAPPDATA 'StreetChem\install-backups')
)
$ErrorActionPreference='Stop'
$cp=(Resolve-Path -LiteralPath $CyberpunkPath).Path
$si=(Resolve-Path -LiteralPath $ScheduleIPath).Path
$cpBin=Join-Path $cp 'bin\x64'
$cpExe=Join-Path $cpBin 'Cyberpunk2077.exe'
$siExe=Join-Path $si 'Schedule I.exe'
foreach($path in @($cpExe,$siExe,(Join-Path $cp 'red4ext\RED4ext.dll'),(Join-Path $si 'MelonLoader\net6\MelonLoader.dll'),(Join-Path $cpBin 'ReShade.ini'))){if(!(Test-Path -LiteralPath $path)){throw "Missing game/dependency: $path. Read docs/INSTALL.md."}}
if(!(Test-Path -LiteralPath (Join-Path $cp 'red4ext\plugins\TweakXL\TweakXL.dll'))){throw 'TweakXL is required for Cyberpunk inventory consumables. Install it from its official project.'}
if(!(Test-Path -LiteralPath (Join-Path $cp 'red4ext\plugins\Codeware\Codeware.dll'))){throw 'Codeware is required for the H10 apartment dealer. Install it from its official project.'}
foreach($process in @(Get-Process Cyberpunk2077,'Schedule I' -ErrorAction SilentlyContinue)){if($process.Path -eq $cpExe -or $process.Path -eq $siExe){throw 'Close both selected games first. This installer does not stop them or change saves.'}}
if(!$ReShadeIncludePath){
 foreach($candidate in @((Join-Path $cpBin 'reshade-shaders\Shaders\ReShade.fxh'),(Join-Path $cpBin 'streetchem\shaders\ReShade.fxh'))){if(Test-Path -LiteralPath $candidate){$ReShadeIncludePath=$candidate;break}}
}
if(!$ReShadeIncludePath -or !(Test-Path -LiteralPath $ReShadeIncludePath)){throw 'Locate ReShade.fxh from your official ReShade Standard effects installation and pass -ReShadeIncludePath.'}
$manifestPath=Join-Path $PSScriptRoot 'FILES.json'
$files=Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
foreach($row in $files){$file=Join-Path $PSScriptRoot $row.path;if(!(Test-Path -LiteralPath $file) -or (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash -ne $row.sha256){throw "Release checksum failed: $($row.path)"}}
$plan=@()
foreach($row in $files){
 if($row.path.StartsWith('payload/cyberpunk/')){$plan+=@{source=(Join-Path $PSScriptRoot $row.path);target=(Join-Path $cp $row.path.Substring(18))}}
 elseif($row.path.StartsWith('payload/schedule-i/')){$plan+=@{source=(Join-Path $PSScriptRoot $row.path);target=(Join-Path $si $row.path.Substring(19))}}
}
if($plan.Count -ne 9){throw 'Unexpected payload layout'}
$hostConfig=Join-Path $cpBin 'ReShade.ini'
$config=Get-Content -LiteralPath $hostConfig -Raw
$match=[regex]::Match($config,'(?m)^PresetPath=(.*)$')
$preset=if($match.Success -and $match.Groups[1].Value.Trim()){$match.Groups[1].Value.Trim()}else{'.\streetchem\StreetChemPreset.ini'}
if(![IO.Path]::IsPathRooted($preset)){$preset=Join-Path $cpBin $preset}
$preset=[IO.Path]::GetFullPath($preset)
function Set-Key([string]$Text,[string]$Key,[string]$Value){
 $expression='(?m)^'+[regex]::Escape($Key)+'=.*$'
 if([regex]::IsMatch($Text,$expression)){return [regex]::Replace($Text,$expression,[System.Text.RegularExpressions.MatchEvaluator]{param($m)$Key+'='+$Value})}
 if($Key -eq 'EffectSearchPaths'){
  if($Text -notmatch '(?m)^\[GENERAL\]'){throw 'ReShade.ini has no GENERAL section'}
  return [regex]::Replace($Text,'(?m)^\[GENERAL\]','[GENERAL]'+[Environment]::NewLine+$Key+'='+$Value)
 }
 return $Key+'='+$Value+[Environment]::NewLine+$Text
}
$effects=[regex]::Match($config,'(?m)^EffectSearchPaths=(.*)$').Groups[1].Value.Trim()
if($effects -notmatch 'streetchem[\\/]shaders'){$effects=($effects.TrimEnd(',')+',.\streetchem\shaders').TrimStart(',')}
$config=Set-Key $config 'EffectSearchPaths' $effects
if(!$match.Success -or !$match.Groups[1].Value.Trim()){
 if($match.Success){$config=Set-Key $config 'PresetPath' '.\streetchem\StreetChemPreset.ini'}
 else {
 if($config -match '(?m)^\[GENERAL\]'){$config=[regex]::Replace($config,'(?m)^\[GENERAL\]','[GENERAL]'+[Environment]::NewLine+'PresetPath=.\streetchem\StreetChemPreset.ini')}
 else {throw 'ReShade.ini has no GENERAL section'}
 }
}
$presetText=if(Test-Path -LiteralPath $preset){Get-Content -LiteralPath $preset -Raw}else{''}
foreach($key in @('Techniques','TechniqueSorting')){
 $value=[regex]::Match($presetText,'(?m)^'+$key+'=(.*)$').Groups[1].Value.Trim()
 if($value -notmatch 'StreetChemLab@StreetChemLab.fx'){$value=($value.TrimEnd(',')+',StreetChemLab@StreetChemLab.fx').TrimStart(',')}
 $presetText=Set-Key $presetText $key $value
}
$changed=@($plan.target)+@((Join-Path $cpBin 'streetchem\shaders\ReShade.fxh'),$hostConfig,$preset)
if(!$PSCmdlet.ShouldProcess(($cp+' and '+$si),'Install nine solo mod files and configure the StreetChem ReShade effect')){return}
$backup=Join-Path $BackupRoot ((Get-Date -Format 'yyyyMMdd-HHmmss')+'-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $backup -Force | Out-Null
$journal=@();$index=0
foreach($target in ($changed | Select-Object -Unique)){
 $existed=Test-Path -LiteralPath $target;$saved="$index.bak"
 if($existed){Copy-Item -LiteralPath $target -Destination (Join-Path $backup $saved)}
 $journal+=@{path=$target;existed=$existed;backup=$saved;installedHash='';gameRoots=@($cp,$si)};$index++
}
$journal | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $backup 'restore.json')
try {
 foreach($row in $plan){New-Item -ItemType Directory -Path (Split-Path -Parent $row.target) -Force | Out-Null;Copy-Item -LiteralPath $row.source -Destination $row.target -Force}
 $includeTarget=Join-Path $cpBin 'streetchem\shaders\ReShade.fxh'
 if([IO.Path]::GetFullPath($ReShadeIncludePath) -ne $includeTarget){Copy-Item -LiteralPath $ReShadeIncludePath -Destination $includeTarget -Force}
 New-Item -ItemType Directory -Path (Split-Path -Parent $preset) -Force | Out-Null
 Set-Content -LiteralPath $hostConfig -Value $config -NoNewline
 Set-Content -LiteralPath $preset -Value $presetText -NoNewline
 foreach($row in $journal){$row.installedHash=(Get-FileHash -LiteralPath $row.path -Algorithm SHA256).Hash}
 $journal | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $backup 'restore.json')
}catch {
 foreach($row in $journal){if($row.existed){Copy-Item -LiteralPath (Join-Path $backup $row.backup) -Destination $row.path -Force}else{if(Test-Path -LiteralPath $row.path){Remove-Item -LiteralPath $row.path}}}
 throw
}
Write-Output "Installed Street Chem solo v$((Get-Content -LiteralPath (Join-Path $PSScriptRoot 'VERSION') -Raw).Trim()). Backup: $backup"
Write-Output 'Start both games and load paired saves. Read docs/CONTROLS.md. No saves were modified.'
