param([Parameter(Mandatory)][string]$ModKitPath)
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$version=(Get-Content -LiteralPath (Join-Path $root 'VERSION') -Raw).Trim()
$kit=(Resolve-Path -LiteralPath $ModKitPath).Path
$payload=@(
 @{game='Cyberpunk2077';source='native/build/StreetChemHost.dll';dest='red4ext/plugins/StreetChem/StreetChemHost.dll'},
 @{game='Cyberpunk2077';source='native/build/StreetChemRender.addon64';dest='bin/x64/StreetChemRender.addon64'},
 @{game='Cyberpunk2077';source='native/StreetChemLab.fx';dest='bin/x64/streetchem/shaders/StreetChemLab.fx'},
 @{game='Cyberpunk2077';source='nivalis/native/build/NivalisNightCityHost.dll';dest='red4ext/plugins/NivalisNightCity/NivalisNightCityHost.dll'},
 @{game='ScheduleI';source='guest/bin/Release/net6.0/StreetChem.Guest.dll';dest='Mods/StreetChem.Guest.dll'},
 @{game='NivalisNights';source='nivalis/guest/bin/Release/net6.0/NivalisNightCity.dll';dest='BepInEx/plugins/NivalisNightCity.dll'},
 @{game='NivalisNights';absolute=$kit;dest='BepInEx/plugins/NivalisModKit.dll'}
)
foreach($folder in @(@{source='host';dest='StreetChem'},@{source='nivalis/host';dest='NivalisNightCity'})){
 foreach($script in Get-ChildItem -LiteralPath (Join-Path $root $folder.source) -Filter '*.reds'){
  $payload+=@{game='Cyberpunk2077';absolute=$script.FullName;dest=('r6/scripts/'+$folder.dest+'/'+$script.Name)}
 }
}
foreach($row in $payload){if(!$row.absolute){$row.absolute=Join-Path $root $row.source};if(!(Test-Path -LiteralPath $row.absolute)){throw "Build first: $($row.source)"}}
$counts=@{Cyberpunk2077=12;ScheduleI=1;NivalisNights=2}
foreach($game in @('Cyberpunk2077','ScheduleI','NivalisNights')){
 $stage=Join-Path $root ('dist/CyberTrap-'+$game+'-v'+$version)
 if((Test-Path -LiteralPath $stage) -or (Test-Path -LiteralPath ($stage+'.zip'))){throw "Stage exists; preserve the old build and select a fresh version: $stage"}
 $selected=@($payload|Where-Object game -EQ $game)
 if($selected.Count -ne $counts[$game]){throw "Unexpected $game payload count: $($selected.Count)"}
 New-Item -ItemType Directory -Path $stage|Out-Null
 foreach($row in $selected){$target=Join-Path $stage $row.dest;New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force|Out-Null;Copy-Item -LiteralPath $row.absolute -Destination $target}
 $docs=Join-Path $stage 'CyberTrap-Docs'
 New-Item -ItemType Directory -Path $docs|Out-Null
 foreach($name in @('README.md','LICENSE','CREDITS.md','THIRD_PARTY_NOTICES.md','CHANGELOG.md','VERSION')){Copy-Item -LiteralPath (Join-Path $root $name) -Destination $docs}
 Copy-Item -LiteralPath (Join-Path $root 'docs') -Destination (Join-Path $docs 'docs') -Recurse
 if($game -eq 'NivalisNights'){
  New-Item -ItemType Directory -Path (Join-Path $docs 'tools')|Out-Null
  Copy-Item -LiteralPath (Join-Path $root 'nivalis/tools/guest_query.py') -Destination (Join-Path $docs 'tools/guest_query.py')
 }
 if($game -eq 'Cyberpunk2077'){
  $converter=Join-Path $docs 'tools/IconCooker';New-Item -ItemType Directory -Path $converter -Force|Out-Null
  Copy-Item -LiteralPath (Join-Path $root 'tools/BuildLocalIcons.ps1') -Destination (Join-Path $docs 'tools/BuildLocalIcons.ps1')
  foreach($name in @('IconCooker.dll','IconCooker.deps.json','IconCooker.runtimeconfig.json')){Copy-Item -LiteralPath (Join-Path $root ('tools/IconCooker/bin/Release/net10.0/'+$name)) -Destination $converter}
  foreach($name in @('Program.cs','IconCooker.csproj','LICENSE')){Copy-Item -LiteralPath (Join-Path $root ('tools/IconCooker/'+$name)) -Destination $converter}
 }
 $files=Get-ChildItem -LiteralPath $stage -Recurse -File|ForEach-Object {@{path=[IO.Path]::GetRelativePath($stage,$_.FullName).Replace('\','/');sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}}
 $files|Sort-Object path|ConvertTo-Json -Depth 4|Set-Content -LiteralPath (Join-Path $docs 'FILES.json') -Encoding utf8
 Compress-Archive -Path (Join-Path $stage '*') -DestinationPath ($stage+'.zip')
 $hash=(Get-FileHash -LiteralPath ($stage+'.zip') -Algorithm SHA256).Hash
 ($hash+'  '+[IO.Path]::GetFileName($stage+'.zip'))|Set-Content -LiteralPath ($stage+'.zip.sha256') -Encoding ascii
 Write-Output ($stage+'.zip ('+$hash+')')
}
