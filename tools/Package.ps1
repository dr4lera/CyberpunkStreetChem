param()
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$version=(Get-Content -LiteralPath (Join-Path $root 'VERSION') -Raw).Trim()
$stage=Join-Path $root ('dist\StreetChem-Solo-v'+$version)
if(Test-Path -LiteralPath $stage){throw "Package stage already exists: $stage. Use a fresh version or move it aside."}
$payload=@(
 @{source='native\build\StreetChemHost.dll';dest='payload/cyberpunk/red4ext/plugins/StreetChem/StreetChemHost.dll'},
 @{source='native\build\StreetChemRender.addon64';dest='payload/cyberpunk/bin/x64/StreetChemRender.addon64'},
 @{source='native\StreetChemLab.fx';dest='payload/cyberpunk/bin/x64/streetchem/shaders/StreetChemLab.fx'},
 @{source='host\StreetChemCity.reds';dest='payload/cyberpunk/r6/scripts/StreetChem/StreetChemCity.reds'},
 @{source='host\StreetChemVisual.reds';dest='payload/cyberpunk/r6/scripts/StreetChem/StreetChemVisual.reds'},
 @{source='host\StreetChemRunners.reds';dest='payload/cyberpunk/r6/scripts/StreetChem/StreetChemRunners.reds'},
 @{source='guest\bin\Release\net6.0\StreetChem.Guest.dll';dest='payload/schedule-i/Mods/StreetChem.Guest.dll'}
)
foreach($row in $payload){if(!(Test-Path -LiteralPath (Join-Path $root $row.source))){throw "Build first. Missing: $($row.source)"}}
New-Item -ItemType Directory -Path $stage -Force | Out-Null
foreach($name in @('README.md','CHANGELOG.md','CREDITS.md','LICENSE','THIRD_PARTY_NOTICES.md','VERSION')){Copy-Item -LiteralPath (Join-Path $root $name) -Destination (Join-Path $stage $name)}
Copy-Item -LiteralPath (Join-Path $root 'docs') -Destination (Join-Path $stage 'docs') -Recurse
foreach($name in @('Install.ps1','Restore.ps1')){Copy-Item -LiteralPath (Join-Path $root ('installer\'+$name)) -Destination (Join-Path $stage $name)}
foreach($row in $payload){$dest=Join-Path $stage $row.dest;New-Item -ItemType Directory -Path (Split-Path -Parent $dest) -Force | Out-Null;Copy-Item -LiteralPath (Join-Path $root $row.source) -Destination $dest}
$rows=Get-ChildItem -LiteralPath $stage -File -Recurse | ForEach-Object {@{path=[IO.Path]::GetRelativePath($stage,$_.FullName).Replace('\','/');sha256=(Get-FileHash -LiteralPath $_.FullName).Hash}}
$rows | Sort-Object path | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $stage 'FILES.json')
$zip=Join-Path $root ('dist\StreetChem-Solo-v'+$version+'.zip')
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip
$hash=(Get-FileHash -LiteralPath $zip).Hash
($hash+'  '+[IO.Path]::GetFileName($zip)) | Set-Content -LiteralPath ($zip+'.sha256')
Write-Output "$zip ($hash)"
