# Builds, tests and packages the mod with standard tools: .NET SDK/MSBuild, NUnit/VSTest and
# Thunderstore CLI (tcli). The result is a Thunderstore package that Gale or r2modman can
# import. Never writes to the game or a mod-manager profile.
# Paths not passed as parameters are read from TOLMACH_GAME_PATH, TOLMACH_PROFILE_PATH,
# TOLMACH_BEPINEX_CORE_PATH and TOLMACH_NEWTONSOFT_JSON_PATH, then asked for interactively.
# -OutputDirectory (used by release-kit) receives the package and its SHA256SUMS instead
# of artifacts/; the directory must not exist yet.
# Intermediate files and validation logs use a fresh project tmp directory, or -WorkDirectory.
[CmdletBinding()]
param(
    [string]$GamePath = $env:TOLMACH_GAME_PATH,
    [string]$ProfilePath = $env:TOLMACH_PROFILE_PATH,
    [string]$BepInExCorePath = $env:TOLMACH_BEPINEX_CORE_PATH,
    [string]$NewtonsoftJsonPath = $env:TOLMACH_NEWTONSOFT_JSON_PATH,
    [string]$OutputDirectory,
    [string]$WorkDirectory,
    [string]$DotnetExecutable = 'dotnet'
)
Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'
$Root = $PSScriptRoot
$stage = $null
$stagePackage = $null
$interactive = -not ([Environment]::GetCommandLineArgs() -contains '-NonInteractive')
function Existing-Folder([string]$Path, [string]$Label) {
    if (-not $Path) {
        if (-not $interactive) { throw "$Label is not set. Pass it as a parameter or set the TOLMACH_* environment variable." }
        $Path = Read-Host $Label
    }
    $value = (Resolve-Path -LiteralPath $Path -ErrorAction Stop).Path
    if (-not (Test-Path -LiteralPath $value -PathType Container)) { throw "Not a directory: $value" }
    return $value
}
function Run-Dotnet([string[]]$Arguments, [string]$LogPath) {
    $previous = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        & $DotnetExecutable @Arguments 2>&1 | Tee-Object -FilePath $LogPath | ForEach-Object { Write-Host $_ }
        $code = $LASTEXITCODE
    } finally { $ErrorActionPreference = $previous }
    if ($code -ne 0) { throw "dotnet $($Arguments[0]) failed (exit $code). Log: $LogPath" }
}
# .NET instead of Get-FileHash: that cmdlet lives in the script part of the Utility module,
# which Windows PowerShell fails to load when a PowerShell 7 PSModulePath is inherited
# through another process (for example release-kit started from pwsh).
function Sha256([string]$Path) {
    $sha = [Security.Cryptography.SHA256]::Create()
    $stream = [IO.File]::OpenRead($Path)
    try { return (($sha.ComputeHash($stream) | ForEach-Object { $_.ToString('x2') }) -join '') } finally { $stream.Dispose(); $sha.Dispose() }
}
function Entry-Bytes($Entry) {
    $stream = $Entry.Open()
    try { $memory = New-Object IO.MemoryStream; $stream.CopyTo($memory); return ,$memory.ToArray() } finally { $stream.Dispose() }
}
function Bytes-Sha256([byte[]]$Bytes) {
    $sha = [Security.Cryptography.SHA256]::Create()
    try { return (($sha.ComputeHash($Bytes) | ForEach-Object { $_.ToString('x2') }) -join '') } finally { $sha.Dispose() }
}
# Thunderstore package rules (root icon.png 256x256, README.md, manifest.json; name, version,
# description and dependency formats) plus the layout Gale's BepInEx installer needs: files
# outside plugins/ are flattened, so everything the plugin loads must be under plugins/.
function Test-Package([string]$Zip, [string]$Version, [string]$DllHash, $CatalogHashes) {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    Add-Type -AssemblyName System.Drawing
    $archive = [IO.Compression.ZipFile]::OpenRead($Zip)
    try {
        $entries = @{}
        foreach ($entry in $archive.Entries) {
            if ($entry.FullName.Contains('\')) { throw "Archive entry uses a backslash: $($entry.FullName)" }
            if ($entry.FullName.EndsWith('/')) { continue }
            if ($entries.ContainsKey($entry.FullName)) { throw "Duplicate archive entry: $($entry.FullName)" }
            $entries[$entry.FullName] = $entry
        }
        $expected = @('manifest.json', 'icon.png', 'README.md', 'CHANGELOG.md', 'LICENSE', 'build-receipt.json', 'plugins/Tolmach.dll')
        $expected += @($CatalogHashes.Keys | ForEach-Object { "plugins/catalog/$_" })
        $missing = @($expected | Where-Object { -not $entries.ContainsKey($_) })
        $extra = @($entries.Keys | Where-Object { $expected -notcontains $_ })
        if ($missing.Count -or $extra.Count) { throw "Unexpected package layout. Missing: $($missing -join ', '). Extra: $($extra -join ', ')" }
        $manifestBytes = Entry-Bytes $entries['manifest.json']
        if ($manifestBytes.Length -ge 3 -and $manifestBytes[0] -eq 0xEF -and $manifestBytes[1] -eq 0xBB -and $manifestBytes[2] -eq 0xBF) { throw 'manifest.json must not start with a UTF-8 BOM.' }
        $strict = New-Object Text.UTF8Encoding($false, $true)
        $manifest = $strict.GetString($manifestBytes) | ConvertFrom-Json
        if ($manifest.name -notmatch '^[A-Za-z0-9_]{1,128}$') { throw "Invalid manifest name: $($manifest.name)" }
        if ($manifest.version_number -ne $Version -or $manifest.version_number -notmatch '^\d+\.\d+\.\d+$') { throw "Manifest version $($manifest.version_number) differs from $Version." }
        if ($null -eq $manifest.PSObject.Properties['website_url'] -or $manifest.website_url -isnot [string]) { throw 'manifest.json needs website_url (may be empty).' }
        if ([string]::IsNullOrEmpty($manifest.description) -or $manifest.description.Length -gt 250) { throw 'Manifest description must have 1..250 characters.' }
        foreach ($dependency in @($manifest.dependencies)) {
            if ($dependency -notmatch '^[A-Za-z0-9_]+-[A-Za-z0-9_]+-\d+\.\d+\.\d+$') { throw "Invalid dependency: $dependency" }
        }
        # System.Drawing decodes the whole image, so a PNG signature on other data does not pass.
        $iconBytes = Entry-Bytes $entries['icon.png']
        $iconStream = New-Object IO.MemoryStream -ArgumentList (,$iconBytes)
        try {
            try { $image = [Drawing.Image]::FromStream($iconStream) } catch { throw 'icon.png is not an image.' }
            try {
                if (-not $image.RawFormat.Equals([Drawing.Imaging.ImageFormat]::Png)) { throw 'icon.png is not a PNG file.' }
                if ($image.Width -ne 256 -or $image.Height -ne 256) { throw "icon.png must be 256x256, found $($image.Width)x$($image.Height)." }
            } finally { $image.Dispose() }
        } finally { $iconStream.Dispose() }
        [void]$strict.GetString((Entry-Bytes $entries['README.md']))
        [void]$strict.GetString((Entry-Bytes $entries['CHANGELOG.md']))
        if ((Bytes-Sha256 (Entry-Bytes $entries['plugins/Tolmach.dll'])) -ne $DllHash) { throw 'Packaged DLL differs from the tested DLL.' }
        foreach ($name in $CatalogHashes.Keys) {
            if ((Bytes-Sha256 (Entry-Bytes $entries["plugins/catalog/$name"])) -ne $CatalogHashes[$name]) { throw "Packaged catalog differs from its tested copy: $name" }
        }
        $receipt = $strict.GetString((Entry-Bytes $entries['build-receipt.json'])) | ConvertFrom-Json
        if ($receipt.plugin_sha256 -ne $DllHash -or $receipt.version -ne $Version) { throw 'build-receipt.json does not describe the packaged DLL.' }
        return $manifest
    } finally { $archive.Dispose() }
}
try {
    if (-not (Get-Command $DotnetExecutable -ErrorAction SilentlyContinue)) { throw '.NET SDK 10 is required.' }
    $sdk = (& $DotnetExecutable --version | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or [int]($sdk.Split('.')[0]) -lt 10) { throw '.NET SDK 10 is required (compiler only; the plugin still targets net48).' }
    if ($OutputDirectory) {
        $OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
        if (Test-Path -LiteralPath $OutputDirectory) { throw "Output directory already exists: $OutputDirectory" }
    }
    if (-not $WorkDirectory) {
        $WorkDirectory = Join-Path $Root ('tmp/build/' + [Guid]::NewGuid().ToString('N'))
    }
    $WorkDirectory = [IO.Path]::GetFullPath($WorkDirectory)
    if (Test-Path -LiteralPath $WorkDirectory) { throw "Working directory already exists: $WorkDirectory" }
    if ($WorkDirectory -match '[;,%\r\n]') { throw 'Working directory must not contain MSBuild separators or newlines.' }
    for ($cursor=$WorkDirectory; $cursor; $cursor=[IO.Path]::GetDirectoryName($cursor)) {
        if ((Test-Path -LiteralPath $cursor) -and ((Get-Item -LiteralPath $cursor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw "Linked work path: $cursor" }
    }
    [IO.Directory]::CreateDirectory($WorkDirectory) | Out-Null
    $GamePath = Existing-Folder $GamePath 'Valheim installation directory'
    $ProfilePath = Existing-Folder $ProfilePath 'Mod-manager profile directory (or its BepInEx directory)'
    if ((Split-Path $ProfilePath -Leaf) -ieq 'BepInEx') { $ProfilePath = Split-Path $ProfilePath -Parent }
    if (-not $BepInExCorePath) { $BepInExCorePath = Join-Path $ProfilePath 'BepInEx\core' }
    $BepInExCorePath = Existing-Folder $BepInExCorePath 'BepInEx core directory'
    if (-not $NewtonsoftJsonPath) {
        $plugins = Join-Path $ProfilePath 'BepInEx\plugins'
        $candidates = @(Get-ChildItem -LiteralPath $plugins -Recurse -File -Filter 'Newtonsoft.Json.dll' | Where-Object {
            try { [Reflection.AssemblyName]::GetAssemblyName($_.FullName).Version.Major -eq 13 } catch { $false }
        })
        if ($candidates.Count -ne 1) { throw "Found $($candidates.Count) Newtonsoft.Json 13.x candidates. Pass -NewtonsoftJsonPath explicitly; no arbitrary candidate is selected." }
        $NewtonsoftJsonPath = $candidates[0].FullName
    }
    $NewtonsoftJsonPath = (Resolve-Path -LiteralPath $NewtonsoftJsonPath).Path
    if ([Reflection.AssemblyName]::GetAssemblyName($NewtonsoftJsonPath).Version.Major -ne 13) { throw 'Newtonsoft.Json 13.x is required.' }
    # Each value is passed as one native argument, not as executable shell text.
    # MSBuild separators/escapes need a props file; reject them instead of silently
    # interpreting a different path/property. Spaces and Cyrillic are allowed.
    foreach ($path in @($Root, $GamePath, $ProfilePath, $BepInExCorePath, $NewtonsoftJsonPath)) {
        if ($path -match '[;,%\r\n]') { throw 'Build-script paths must not contain semicolon, comma, percent or newline. Use a directory junction with a plain name for such paths.' }
    }
    $props = @("-p:GamePath=$GamePath", "-p:ProfilePath=$ProfilePath", "-p:BepInExCorePath=$BepInExCorePath", "-p:NewtonsoftJsonPath=$NewtonsoftJsonPath")
    $project = [xml](Get-Content -LiteralPath (Join-Path $Root 'Tolmach.csproj') -Raw -Encoding UTF8)
    $version = [string]$project.SelectSingleNode('/Project/PropertyGroup/Version').InnerText
    if ($version -notmatch '^\d+\.\d+\.\d+$') { throw "Tolmach.csproj <Version> must be Major.Minor.Patch, found '$version'." }
    $artifacts = Join-Path $Root 'artifacts'
    $stage = Join-Path $WorkDirectory 'stage'
    $stagePackage = Join-Path $WorkDirectory 'stage-package'
    # An earlier package must not be mistaken for this attempt's result.
    $previousDir = Join-Path $artifacts 'previous'
    foreach ($old in @(Get-ChildItem -LiteralPath $artifacts -File -Filter "*-Tolmach-$version.zip" -ErrorAction SilentlyContinue | Where-Object { -not $OutputDirectory })) {
        [IO.Directory]::CreateDirectory($previousDir) | Out-Null
        $moved = Join-Path $previousDir ($old.BaseName + '-' + [Guid]::NewGuid().ToString('N') + '.zip')
        Move-Item -LiteralPath $old.FullName -Destination $moved
        Write-Host "Previous package preserved: $moved"
    }
    $run = Join-Path $WorkDirectory 'results'
    [IO.Directory]::CreateDirectory($run) | Out-Null
    $dotnetArtifacts = Join-Path $WorkDirectory 'dotnet'
    # No test filter/skip switch. Compiler errors and genuine Harmony test failures
    # block packaging. NuGet may download pinned DEVELOPMENT dependencies on restore.
    Run-Dotnet (@('test', (Join-Path $Root 'tests\Tolmach.Tests.csproj'), '-c', 'Release',
        '--artifacts-path', $dotnetArtifacts, '--logger', 'trx;LogFileName=tests.trx', '--results-directory', $run, '--verbosity', 'minimal') + $props) (Join-Path $run 'dotnet-test.log')
    $trxPath = Join-Path $run 'tests.trx'
    Run-Dotnet (@('test', (Join-Path $Root 'tests/Runtime.Contracts/Runtime.Contracts.csproj'), '-c', 'Release',
        '--artifacts-path', $dotnetArtifacts, '--logger', 'trx;LogFileName=api.trx', '--results-directory', $run) + $props) (Join-Path $run 'api-test.log')
    [xml]$apiReport = Get-Content -LiteralPath (Join-Path $run 'api.trx') -Raw
    $apiCounters = $apiReport.SelectSingleNode("//*[local-name()='Counters']")
    if ($null -eq $apiCounters -or [int]$apiCounters.total -le 0 -or
        [int]$apiCounters.passed -ne [int]$apiCounters.total -or [int]$apiCounters.executed -ne [int]$apiCounters.total) {
        throw 'Installed API tests were not all executed and passed.'
    }
    if (-not (Test-Path -LiteralPath $trxPath -PathType Leaf)) { throw 'No VSTest result file. Nothing will be packaged.' }
    [xml]$trx = Get-Content -LiteralPath $trxPath -Raw
    $counters = $trx.SelectSingleNode("//*[local-name()='Counters']")
    if ($null -eq $counters -or [int]$counters.executed -le 0 -or [int]$counters.failed -ne 0 -or
        [int]$counters.passed -ne [int]$counters.total -or [int]$counters.executed -ne [int]$counters.total) {
        throw 'Tests were not all executed and passed. Empty discovery, failures and skipped tests block packaging.'
    }
    $dll = Join-Path $dotnetArtifacts 'bin\Tolmach\release\Tolmach.dll'
    $identity = [Reflection.AssemblyName]::GetAssemblyName($dll)
    if ($identity.Name -ne 'Tolmach' -or $identity.Version.ToString() -ne ($version + '.0')) { throw 'Built assembly does not match the project version.' }
    $inputHashes = Join-Path $dotnetArtifacts 'obj\Tolmach\release\reference-hashes.txt'
    if (-not (Test-Path -LiteralPath $inputHashes)) { throw 'MSBuild reference-hash report is missing.' }
    $testOutput = Join-Path $dotnetArtifacts 'bin\Tolmach.Tests\release'
    $testedDll = Join-Path $testOutput 'Tolmach.dll'
    $dllHash = Sha256 $dll
    if ((Sha256 $testedDll) -ne $dllHash) { throw 'Tested DLL differs from the plugin build output.' }
    $testedCatalog = @(Get-ChildItem -LiteralPath (Join-Path $testOutput 'catalog') -File -Filter '*.json')
    $catalog = @(Get-ChildItem -LiteralPath (Join-Path $Root 'catalog') -File -Filter '*.json' | Sort-Object Name)
    # The binding snapshot lists every module; Windows PowerShell emits a JSON array as one object.
    $snapshot = Get-Content -Raw -LiteralPath (Join-Path $Root 'tests\fixtures\snapshot-bindings.json') | ConvertFrom-Json
    if ($catalog.Count -eq 0 -or $catalog.Count -ne @($snapshot).Count -or $testedCatalog.Count -ne $catalog.Count) { throw 'Catalog set is incomplete or differs from test output.' }
    $catalogHashes = [ordered]@{}
    foreach ($file in $catalog) {
        $hash = Sha256 $file.FullName
        if ((Sha256 (Join-Path (Join-Path $testOutput 'catalog') $file.Name)) -ne $hash) { throw "Catalog changed after its test copy: $($file.Name)" }
        $catalogHashes[$file.Name] = $hash
    }
    # Plain strings only: Windows PowerShell serializes Get-Content's extended properties.
    $receipt = [ordered]@{
        package = 'Tolmach'; version = $version; utc = [DateTime]::UtcNow.ToString('o')
        sdk = $sdk
        plugin_assembly_version = $identity.Version.ToString()
        plugin_sha256 = $dllHash
        nunit_passed = [int]$counters.passed
        api_contracts_passed = [int]$apiCounters.passed
        test_scope = 'Compiled production plugin and profile Harmony against managed endpoints; not Unity gameplay.'
        game_test = 'not run'
        references = [string[]][IO.File]::ReadAllLines($inputHashes)
        catalog_sha256 = $catalogHashes
    }
    $payload = Join-Path $stage 'plugins'
    [IO.Directory]::CreateDirectory((Join-Path $payload 'catalog')) | Out-Null
    Copy-Item -LiteralPath $testedDll -Destination $payload
    foreach ($file in $testedCatalog) { Copy-Item -LiteralPath $file.FullName -Destination (Join-Path $payload 'catalog') }
    $receiptJson = $receipt | ConvertTo-Json -Depth 4
    [IO.File]::WriteAllText((Join-Path $stage 'build-receipt.json'), $receiptJson, (New-Object Text.UTF8Encoding($false)))
    [IO.File]::WriteAllText((Join-Path $run 'build-receipt.json'), $receiptJson, (New-Object Text.UTF8Encoding($false)))
    # TCLI resolves inputs relative to its config directory; stage the four package documents there.
    $packageConfig = [IO.File]::ReadAllText((Join-Path $Root 'thunderstore.toml'))
    $packageConfig = $packageConfig.Replace('./artifacts/stage-package', './stage-package')
    $packageConfig = $packageConfig.Replace('./artifacts/stage', './stage')
    [IO.Directory]::CreateDirectory((Join-Path $WorkDirectory 'package')) | Out-Null
    foreach ($relative in @('package/icon.png', 'package/README.md', 'CHANGELOG.md', 'LICENSE')) {
        Copy-Item -LiteralPath (Join-Path $Root $relative) -Destination (Join-Path $WorkDirectory $relative)
    }
    $packageConfigPath = Join-Path $WorkDirectory 'thunderstore.toml'
    [IO.File]::WriteAllText($packageConfigPath, $packageConfig, (New-Object Text.UTF8Encoding($false)))
    # Thunderstore CLI writes manifest.json from thunderstore.toml and assembles the ZIP.
    Push-Location $Root
    try {
        Run-Dotnet @('tool', 'restore') (Join-Path $run 'tool-restore.log')
        Run-Dotnet @('tool', 'run', 'tcli', '--', 'build', '--config-path', $packageConfigPath, '--package-version', $version) (Join-Path $run 'tcli-build.log')
    } finally { Pop-Location }
    $built = @(Get-ChildItem -LiteralPath $stagePackage -File -Filter '*.zip')
    if ($built.Count -ne 1) { throw "Expected one package from tcli, found $($built.Count)." }
    $manifest = Test-Package $built[0].FullName $version $dllHash $catalogHashes
    if ($OutputDirectory) {
        [IO.Directory]::CreateDirectory($OutputDirectory) | Out-Null
        $package = Join-Path $OutputDirectory $built[0].Name
        Move-Item -LiteralPath $built[0].FullName -Destination $package
        $sums = "$(Sha256 $package)  $($built[0].Name)`n"
        [IO.File]::WriteAllText((Join-Path $OutputDirectory 'SHA256SUMS'), $sums, (New-Object Text.UTF8Encoding($false)))
    } else {
        [IO.Directory]::CreateDirectory($artifacts) | Out-Null
        $package = Join-Path $artifacts $built[0].Name
        Move-Item -LiteralPath $built[0].FullName -Destination $package
    }
    Write-Host "Package: $package"
    Write-Host "Manifest: $($manifest.name) $($manifest.version_number); dependencies: $(@($manifest.dependencies) -join ', ')"
    Write-Host "Test/build evidence: $run"
    Write-Host 'Game/profile unchanged. Import the ZIP in Gale (Import > ...local mod, or drop it on the window) or r2modman (Settings > Import local mod).'
    exit 0
} catch {
    Write-Error $_ -ErrorAction Continue
    exit 1
}
