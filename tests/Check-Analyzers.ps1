#Requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Project,
    [Parameter(Mandatory)][string]$WorkDirectory,
    [string]$DotnetExecutable = 'dotnet',
    [string]$GamePath,
    [string]$ProfilePath,
    [string]$JotunnPath
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$owner = Split-Path $PSScriptRoot -Parent
$Project = [IO.Path]::GetFullPath($Project)
$WorkDirectory = [IO.Path]::GetFullPath($WorkDirectory)
$boundary = $owner.TrimEnd('\','/') + [IO.Path]::DirectorySeparatorChar
if (-not $Project.StartsWith($boundary, [StringComparison]::OrdinalIgnoreCase)) { throw 'Project must belong to this repository.' }
$allowed = @('tmp','artifacts') | ForEach-Object { (Join-Path $owner $_).TrimEnd('\','/') + [IO.Path]::DirectorySeparatorChar }
if (-not @($allowed | Where-Object { $WorkDirectory.StartsWith($_, [StringComparison]::OrdinalIgnoreCase) }).Count) { throw 'Use this repository tmp or artifacts for controls.' }
foreach ($path in @($Project, $WorkDirectory)) {
    for ($cursor=$path; $cursor; $cursor=[IO.Path]::GetDirectoryName($cursor)) {
        if ((Test-Path -LiteralPath $cursor) -and ((Get-Item -LiteralPath $cursor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw "Linked path: $cursor" }
    }
}
if (Test-Path -LiteralPath $WorkDirectory) { throw 'Use a fresh WorkDirectory.' }
New-Item -ItemType Directory -Path $WorkDirectory | Out-Null
$properties = @()
foreach ($pair in @(@('GamePath',$GamePath), @('ProfilePath',$ProfilePath), @('JotunnPath',$JotunnPath))) {
    if ($pair[1]) { $properties += "-p:$($pair[0])=$($pair[1])" }
}
$controls = [ordered]@{EARLY_CHECK_VALID=$null; EARLY_CHECK_BAD_DOTNET='CA2013'; EARLY_CHECK_BAD_UNITY='UNT0008'; EARLY_CHECK_BAD_HARMONY='HARMONIZE001'}
Push-Location -LiteralPath $owner
try {
    foreach ($control in $controls.GetEnumerator()) {
        $build = Join-Path $WorkDirectory $control.Key
        $arguments = @('build',$Project,'-c','Release','--artifacts-path',$build,
            "-p:AgentLabBuildRoot=$build","-p:LivingWorldTemp=$build","-p:EarlyCheckControl=$($control.Key)") + $properties
        $log = & $DotnetExecutable @arguments 2>&1 | Out-String
        $code = $LASTEXITCODE
        [IO.File]::WriteAllText((Join-Path $WorkDirectory ($control.Key + '.log')), $log, [Text.UTF8Encoding]::new($false))
        if ($null -eq $control.Value) {
            if ($code -ne 0) { throw "Valid analyzer control failed. See $WorkDirectory." }
        } elseif ($code -eq 0 -or $log -notmatch ("error " + $control.Value + '\b')) {
            throw "Negative control must fail with $($control.Value). See $WorkDirectory."
        }
        if ($log -match '\b(AD0001|CS8032|CS9057)\b') { throw "Analyzer failed to load or execute. See $WorkDirectory." }
        Write-Host "Analyzer control passed: $($control.Key)"
    }
} finally { Pop-Location }

