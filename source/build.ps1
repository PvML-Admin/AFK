$ErrorActionPreference = 'Stop'

function Get-PinnedPackage($packages, $id, $version, $dllRelativePath) {
    $dest = Join-Path $packages "$id.$version"
    $dll = Join-Path $dest $dllRelativePath
    if (Test-Path -LiteralPath $dll) { return $dll }
    New-Item -ItemType Directory -Force -Path $packages | Out-Null
    if (Test-Path -LiteralPath $dest) { Remove-Item -LiteralPath $dest -Recurse -Force }
    $nupkg = Join-Path $packages "$id.$version.nupkg"
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    Invoke-WebRequest -Uri "https://www.nuget.org/api/v2/package/$id/$version" -OutFile $nupkg
    $zip = Join-Path $packages "$id.$version.zip"
    Copy-Item -LiteralPath $nupkg -Destination $zip -Force
    Expand-Archive -LiteralPath $zip -DestinationPath $dest -Force
    Remove-Item -LiteralPath $zip -Force
    if (-not (Test-Path -LiteralPath $dll)) { throw "Package $id $version did not contain $dllRelativePath." }
    return $dll
}

function Find-WindowsMetadata($windowsDirectory, $kitRoots) {
    # Prefer the winmd files shipped in System32\WinMetadata
    # (Windows.Foundation, Windows.Graphics, Windows.Media, and so on). Graphics
    # Capture and Direct3D11 are not separate files there, and the toast types
    # live in Windows.UI.winmd rather than Windows.UI.Notifications.winmd.
    $required = @(
        'Windows.Foundation',
        'Windows.Graphics',
        'Windows.Graphics.Capture',
        'Windows.Graphics.DirectX',
        'Windows.Graphics.DirectX.Direct3D11',
        'Windows.UI.Notifications',
        'Windows.Data',
        'Windows.Media',
        'Windows.Globalization'
    )
    if ($windowsDirectory) {
        $winmeta = Join-Path $windowsDirectory 'System32\WinMetadata'
        $local = @()
        $complete = $true
        foreach ($name in $required) {
            $path = Join-Path $winmeta ($name + '.winmd')
            if (Test-Path -LiteralPath $path) { $local += $path } else { $complete = $false }
        }
        if ($complete) {
            foreach ($path in $local) { Write-Output $path }
            return
        }
    }

    # Windows 10 version 2004 is the first SDK that includes IsCursorCaptureEnabled.
    $minimum = [Version]'10.0.19041.0'
    $contracts = @(
        'Windows.Foundation.FoundationContract',
        'Windows.Foundation.UniversalApiContract',
        'Windows.Networking.Connectivity.WwanContract'
    )
    $bestUnion = $null
    $bestUnionVersion = $null
    $bestContracts = $null
    $bestContractVersion = $null
    foreach ($root in @($kitRoots)) {
        if (-not $root) { continue }
        $unionRoot = Join-Path $root 'UnionMetadata'
        if (Test-Path -LiteralPath $unionRoot) {
            $dirs = @(Get-ChildItem -LiteralPath $unionRoot -Directory -ErrorAction SilentlyContinue)
            foreach ($dir in $dirs) {
                $version = $null
                if (-not [Version]::TryParse($dir.Name, [ref]$version)) { continue }
                if ($version -lt $minimum) { continue }
                $winmd = Join-Path $dir.FullName 'Windows.winmd'
                if (-not (Test-Path -LiteralPath $winmd)) { continue }
                if ($null -eq $bestUnionVersion -or $version -gt $bestUnionVersion) {
                    $bestUnionVersion = $version
                    $bestUnion = $winmd
                }
            }
        }
        $refRoot = Join-Path $root 'References'
        if (-not (Test-Path -LiteralPath $refRoot)) { continue }
        $dirs = @(Get-ChildItem -LiteralPath $refRoot -Directory -ErrorAction SilentlyContinue)
        foreach ($dir in $dirs) {
            $version = $null
            if (-not [Version]::TryParse($dir.Name, [ref]$version)) { continue }
            if ($version -lt $minimum) { continue }
            $paths = @()
            $ok = $true
            foreach ($contract in $contracts) {
                $candidate = Join-Path $dir.FullName $contract
                $candidate = Join-Path $candidate $dir.Name
                $candidate = Join-Path $candidate ($contract + '.winmd')
                if (-not (Test-Path -LiteralPath $candidate)) {
                    $hits = @(Get-ChildItem -LiteralPath $dir.FullName -Recurse -Filter ($contract + '.winmd') -File -ErrorAction SilentlyContinue)
                    if ($hits.Count -eq 0) { $ok = $false; break }
                    $candidate = ($hits | Sort-Object -Property FullName -Descending | Select-Object -First 1).FullName
                }
                $paths += $candidate
            }
            if (-not $ok) { continue }
            if ($null -eq $bestContractVersion -or $version -gt $bestContractVersion) {
                $bestContractVersion = $version
                $bestContracts = $paths
            }
        }
    }
    if ($bestUnion) { Write-Output $bestUnion; return }
    # A partial System32 set often has Foundation and Graphics but not
    # Graphics.Capture as its own file. Callers then fall through to the
    # Windows SDK contracts package, which does contain it.
    if ($bestContracts) {
        foreach ($path in $bestContracts) { Write-Output $path }
    }
}

$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$frameworkDirectory = Split-Path -Parent $compiler
if (-not (Test-Path -LiteralPath $compiler)) {
    throw 'The Windows .NET Framework C# compiler was not found. Enable .NET Framework 4.8 or build with a compatible C# compiler.'
}
Push-Location $PSScriptRoot
try {
    $outputDirectory = $PSScriptRoot
    if (-not (Test-Path -LiteralPath (Join-Path $PSScriptRoot 'assets')) -and (Test-Path -LiteralPath (Join-Path $PSScriptRoot '..\assets'))) {
        $outputDirectory = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
    }
    $outputFile = Join-Path $outputDirectory 'KavrosAfkWarden.exe'
    # Velopack 1.2.161 targets net472 and is the update/installer library.
    # Keep this version aligned with the vpk tool in pack-release.ps1.
    $packages = Join-Path $PSScriptRoot 'packages'
    $velopackDll = Get-PinnedPackage $packages 'Velopack' '1.2.161' 'lib\net472\Velopack.dll'
    $jsonDll = Get-PinnedPackage $packages 'Newtonsoft.Json' '13.0.4' 'lib\net45\Newtonsoft.Json.dll'
    $references = @('mscorlib.dll','System.dll','System.Core.dll','System.Drawing.dll','System.Windows.Forms.dll','System.Xml.dll','System.IO.Compression.dll','System.Runtime.WindowsRuntime.dll') |
        ForEach-Object { '/reference:' + (Join-Path $frameworkDirectory $_) }
    $references += '/reference:' + (Join-Path $env:WINDIR 'Microsoft.NET\assembly\GAC_MSIL\System.Runtime\v4.0_4.0.0.0__b03f5f7f11d50a3a\System.Runtime.dll')
    $references += '/reference:' + $velopackDll
    $references += '/reference:' + $jsonDll
    # System32\WinMetadata on a normal Windows 10/11 PC does not contain
    # Windows.Graphics.Capture.winmd. Use the SDK copy when it is installed,
    # otherwise the same Windows 10 2004 contracts package .NET Framework uses.
    $kitRoots = @()
    $programFiles86 = ${env:ProgramFiles(x86)}
    if ($programFiles86) { $kitRoots += (Join-Path $programFiles86 'Windows Kits\10') }
    if ($env:ProgramFiles) {
        $programFilesKit = Join-Path $env:ProgramFiles 'Windows Kits\10'
        if ($kitRoots -notcontains $programFilesKit) { $kitRoots += $programFilesKit }
    }
    $winmds = @(Find-WindowsMetadata $env:WINDIR $kitRoots)
    if ($winmds.Count -eq 0) {
        try {
            $universal = Get-PinnedPackage $packages 'Microsoft.Windows.SDK.Contracts' '10.0.19041.1' 'ref\netstandard2.0\Windows.Foundation.UniversalApiContract.winmd'
        } catch {
            throw ("Windows Graphics Capture metadata was not found in " + (Join-Path $env:WINDIR 'System32\WinMetadata') + " or the Windows SDK, and Microsoft.Windows.SDK.Contracts 10.0.19041.1 could not be downloaded. " + $_.Exception.Message)
        }
        $contractDir = Split-Path -Parent $universal
        foreach ($name in @('Windows.Foundation.FoundationContract','Windows.Foundation.UniversalApiContract','Windows.Networking.Connectivity.WwanContract')) {
            $path = Join-Path $contractDir ($name + '.winmd')
            if (-not (Test-Path -LiteralPath $path)) { throw "Package Microsoft.Windows.SDK.Contracts 10.0.19041.1 did not contain $name.winmd." }
            $winmds += $path
        }
    }
    foreach ($path in $winmds) { $references += '/reference:' + $path }
    $identity = ([xml](Get-Content -LiteralPath 'app.manifest' -Raw)).assembly.assemblyIdentity
    $version = [Version]::Parse([string]$identity.version).ToString(4)
    $metadata = Join-Path ([IO.Path]::GetTempPath()) ('KavrosAfk-Version-' + [Guid]::NewGuid().ToString('N') + '.cs')
    $art = Join-Path $outputDirectory 'assets\branding'
    $icon = Join-Path $art 'afk-warden.ico'
    $crest = Join-Path $art 'kavros-crest.png'
    if (-not (Test-Path -LiteralPath $icon)) { throw "Missing $icon." }
    if (-not (Test-Path -LiteralPath $crest)) { throw "Missing $crest." }
    $resources = @("/win32icon:$icon", "/resource:$icon,Kavros.Branding.afk-warden.ico", "/resource:$crest,Kavros.Branding.kavros-crest.png")
    # Cinzel and Outfit (SIL OFL 1.1).
    foreach ($fontFile in @('CinzelKavros-Regular.ttf','OutfitKavros-Regular.ttf','OutfitKavros-Semibold.ttf')) {
        $resources += "/resource:$(Join-Path $art $fontFile),Kavros.Branding.$fontFile"
    }
    try {
        [IO.File]::WriteAllText($metadata, ('[assembly:System.Reflection.AssemblyVersion("{0}")] [assembly:System.Reflection.AssemblyFileVersion("{0}")]' -f $version))
        & $compiler /nologo /noconfig /nostdlib+ /langversion:5 /codepage:65001 /target:winexe /platform:anycpu /optimize+ "/out:$outputFile" /win32manifest:app.manifest $references $resources $metadata *.cs
        if ($LASTEXITCODE -ne 0) { throw 'Compilation failed.' }
        Copy-Item -LiteralPath $velopackDll -Destination (Join-Path $outputDirectory 'Velopack.dll') -Force
        Copy-Item -LiteralPath $jsonDll -Destination (Join-Path $outputDirectory 'Newtonsoft.Json.dll') -Force
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'KavrosAfkWarden.exe.config') -Destination (Join-Path $outputDirectory 'KavrosAfkWarden.exe.config') -Force
        if (-not (Test-Path -LiteralPath (Join-Path $outputDirectory 'companion.module.json'))) { throw 'Missing companion.module.json next to the built program.' }
    } finally { [IO.File]::Delete($metadata) }
    Write-Output $outputFile
} finally {
    Pop-Location
}
