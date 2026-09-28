# Controls for the package check in Build.ps1: the built package must be accepted and each
# deliberately broken copy rejected. Functions are loaded from Build.ps1 itself, so the
# controls exercise the same code. Run after Build.ps1:
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools\Test-PackageCheck.ps1
# Broken copies are written to a temporary directory and removed afterwards.
param([string]$Package)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$BuildScript = Join-Path $root 'Build.ps1'
if (-not $Package) { $Package = @(Get-ChildItem -LiteralPath (Join-Path $root 'artifacts') -File -Filter '*-Tolmach-*.zip' | Sort-Object LastWriteTime -Descending)[0].FullName }
$Work = Join-Path ([IO.Path]::GetTempPath()) ('Tolmach-package-check-' + [Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($Work) | Out-Null
$ast = [Management.Automation.Language.Parser]::ParseFile($BuildScript, [ref]$null, [ref]$null)
foreach ($f in $ast.FindAll({ param($n) $n -is [Management.Automation.Language.FunctionDefinitionAst] }, $true)) { . ([scriptblock]::Create($f.Extent.Text)) }
Add-Type -AssemblyName System.IO.Compression.FileSystem
Add-Type -AssemblyName System.Drawing
$good = [IO.Compression.ZipFile]::OpenRead($Package)
$receipt = $null; $data = @{}
foreach ($e in $good.Entries) { if (-not $e.FullName.EndsWith('/')) { $data[$e.FullName] = Entry-Bytes $e } }
$good.Dispose()
$receipt = [Text.Encoding]::UTF8.GetString($data['build-receipt.json']) | ConvertFrom-Json
$catalog = [ordered]@{}; foreach ($p in $receipt.catalog_sha256.PSObject.Properties) { $catalog[$p.Name] = $p.Value }
function Write-Zip([string]$Name, [hashtable]$Files) {
    $path = Join-Path $Work "$Name.zip"; if (Test-Path $path) { Remove-Item $path }
    $zip = [IO.Compression.ZipFile]::Open($path, 'Create')
    try { foreach ($k in $Files.Keys) { $s = $zip.CreateEntry($k).Open(); try { $s.Write($Files[$k], 0, $Files[$k].Length) } finally { $s.Dispose() } } } finally { $zip.Dispose() }
    return $path
}
function Variant([scriptblock]$Change) { $copy = @{}; foreach ($k in $data.Keys) { $copy[$k] = $data[$k] }; & $Change $copy; return $copy }
$cases = [ordered]@{
    'valid package' = Variant { param($f) }
    'catalog outside plugins/ (Gale would flatten it)' = Variant { param($f) foreach ($k in @($f.Keys | Where-Object { $_ -like 'plugins/catalog/*' })) { $f[$k.Substring(8)] = $f[$k]; $f.Remove($k) } }
    'manifest with UTF-8 BOM' = Variant { param($f) $f['manifest.json'] = [byte[]](0xEF, 0xBB, 0xBF) + $f['manifest.json'] }
    'icon 128x128' = Variant { param($f) $b = New-Object Drawing.Bitmap 128, 128; $m = New-Object IO.MemoryStream; $b.Save($m, [Drawing.Imaging.ImageFormat]::Png); $f['icon.png'] = $m.ToArray() }
    'description over 250 characters' = Variant { param($f) $j = [Text.Encoding]::UTF8.GetString($f['manifest.json']) | ConvertFrom-Json; $j.description = 'x' * 251; $f['manifest.json'] = (New-Object Text.UTF8Encoding($false)).GetBytes(($j | ConvertTo-Json)) }
    'DLL differs from tested one' = Variant { param($f) $d = [byte[]]$f['plugins/Tolmach.dll'].Clone(); $d[$d.Length - 1] = $d[$d.Length - 1] -bxor 1; $f['plugins/Tolmach.dll'] = $d }
    'missing README.md' = Variant { param($f) $f.Remove('README.md') }
}
$failed = 0
try {
    foreach ($name in $cases.Keys) {
        $zip = Write-Zip ($name -replace '[^A-Za-z0-9]', '_') $cases[$name]
        $accepted = $true
        try { [void](Test-Package $zip $receipt.version $receipt.plugin_sha256 $catalog) } catch { $accepted = $false; $reason = $_.Exception.Message.Split([char]10)[0] }
        $expected = $name -eq 'valid package'
        if ($accepted -ne $expected) { $failed++ }
        $verdict = if ($accepted -eq $expected) { 'ok  ' } else { 'FAIL' }
        if ($accepted) { "$verdict ACCEPTED $name" } else { "$verdict REJECTED $name :: $reason" }
    }
} finally { Remove-Item -LiteralPath $Work -Recurse -Force -ErrorAction Continue }
if ($failed) { Write-Error "$failed package-check control(s) behaved unexpectedly."; exit 1 }
