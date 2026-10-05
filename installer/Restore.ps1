[CmdletBinding(SupportsShouldProcess)]
param([Parameter(Mandatory)][string]$BackupPath)
$ErrorActionPreference='Stop'
$backup=(Resolve-Path -LiteralPath $BackupPath).Path
$rows=Get-Content -LiteralPath (Join-Path $backup 'restore.json') -Raw | ConvertFrom-Json
foreach($process in @(Get-Process Cyberpunk2077,'Schedule I' -ErrorAction SilentlyContinue)){
 foreach($gameRoot in $rows[0].gameRoots){if($process.Path -like ($gameRoot.TrimEnd('\')+'\*')){throw 'Close the selected games before restoring mod files.'}}
}
foreach($row in $rows){
 if(Test-Path -LiteralPath $row.path){if((Get-FileHash -LiteralPath $row.path).Hash -ne $row.installedHash){Write-Warning "Changed since installation; left untouched: $($row.path)";continue}}
 if($PSCmdlet.ShouldProcess($row.path,'Restore pre-install mod file or remove added file')){
  if($row.existed){Copy-Item -LiteralPath (Join-Path $backup $row.backup) -Destination $row.path -Force}
  elseif(Test-Path -LiteralPath $row.path){Remove-Item -LiteralPath $row.path}
 }
}
Write-Output 'Restoration finished. Game saves and StreetChem business state were retained.'
