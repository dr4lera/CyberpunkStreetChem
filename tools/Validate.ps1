param()
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$failures=@()
$required=@('README.md','LICENSE','CREDITS.md','THIRD_PARTY_NOTICES.md','VERSION','CHANGELOG.md','docs\INSTALL.md','docs\CONTROLS.md','docs\TROUBLESHOOTING.md','docs\TESTING.md')
foreach($file in $required){if(!(Test-Path -LiteralPath (Join-Path $root $file))){$failures+="Missing required file: $file"}}
foreach($file in Get-ChildItem -LiteralPath $root -File -Recurse){
 $relative=[IO.Path]::GetRelativePath($root,$file.FullName)
 if($relative -match '(^|[\\/])(bin|obj|build|dist|\.git)([\\/]|$)'){continue}
 if($file.Name -match '^\.env' -or $file.Extension -in @('.dll','.exe','.pdb','.archive','.assets','.bundle')){$failures+="Unexpected source file: $relative";continue}
 $body=Get-Content -LiteralPath $file.FullName -Raw
 if($body -match '\bgh[pousr]_[A-Za-z0-9]{30,}|\bsk-(?:proj-)?[A-Za-z0-9]{32,}|-----BEGIN (RSA |EC |OPENSSH )?PRIVATE KEY-----'){$failures+="Possible secret: $relative"}
 if($relative -ne 'tools\Validate.ps1' -and $body -match '[A-Z]:[/\\]Users[/\\][^/\\\s]+|7656[0-9]{13}|streetchem_overflow_repair|userRequestedTestFunds|GuestOnline|RunnerVisual'){$failures+="Personal/prototype content: $relative"}
 if($file.Extension -eq '.ps1'){
  $tokens=$null;$errors=$null
  [Management.Automation.Language.Parser]::ParseFile($file.FullName,[ref]$tokens,[ref]$errors) | Out-Null
  foreach($error in $errors){$failures+="PowerShell parse error in $relative : $($error.Message)"}
 }
}
if($failures.Count){$failures | Write-Output;throw 'Source validation failed'}
Write-Output 'Source validation passed: documentation, script syntax, personal data, secrets and prototype exclusion.'
