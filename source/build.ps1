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
    $references = @('mscorlib.dll','System.dll','System.Core.dll','System.Drawing.dll','System.Windows.Forms.dll','System.Xml.dll','System.Runtime.WindowsRuntime.dll') |
        ForEach-Object { '/reference:' + (Join-Path $frameworkDirectory $_) }
    $references += '/reference:' + (Join-Path $env:WINDIR 'Microsoft.NET\assembly\GAC_MSIL\System.Runtime\v4.0_4.0.0.0__b03f5f7f11d50a3a\System.Runtime.dll')
    $references += '/reference:' + $velopackDll
    $references += '/reference:' + $jsonDll
    foreach ($winmd in @('Windows.Foundation','Windows.Graphics','Windows.Graphics.Capture','Windows.Graphics.DirectX','Windows.Graphics.DirectX.Direct3D11','Windows.UI.Notifications','Windows.Data')) {
        $path = Join-Path $env:WINDIR ("System32\WinMetadata\" + $winmd + ".winmd")
        if (-not (Test-Path -LiteralPath $path)) { throw "Missing $path. AFK Warden builds on Windows 10 or 11." }
        $references += '/reference:' + $path
    }
    $identity = ([xml](Get-Content -LiteralPath 'app.manifest' -Raw)).assembly.assemblyIdentity
    $version = [Version]::Parse([string]$identity.version).ToString(4)
    $metadata = Join-Path ([IO.Path]::GetTempPath()) ('KavrosAfk-Version-' + [Guid]::NewGuid().ToString('N') + '.cs')
    $art = Join-Path $outputDirectory 'assets\branding'
    $icon = Join-Path $art 'afk-warden.ico'
    $crest = Join-Path $art 'kavros-crest.png'
    if (-not (Test-Path -LiteralPath $icon)) { throw "Missing $icon." }
    if (-not (Test-Path -LiteralPath $crest)) { throw "Missing $crest." }
    $resources = @("/win32icon:$icon", "/resource:$icon,Kavros.Branding.afk-warden.ico", "/resource:$crest,Kavros.Branding.kavros-crest.png")
    # Same Cinzel and Outfit instances Clue Helper embeds (SIL OFL 1.1).
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
