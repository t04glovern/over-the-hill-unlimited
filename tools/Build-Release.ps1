<#
.SYNOPSIS
    Builds both versions of the mod in Release and packages them in artifacts/:
    OverTheHillUnlimited-<version>.zip (BepInEx) and OverTheHillUnlimited-<version>-MelonLoader.zip.

.DESCRIPTION
    Each zip mirrors the game folder, so players can extract it straight into the folder that contains
    "over the hill.exe": BepInEx/plugins/OverTheHillUnlimited/ for BepInEx, and Mods/ for MelonLoader. The version
    comes from <Version> in src/OverTheHillUnlimited.props. The MelonLoader build needs a game folder with MelonLoader
    installed: -MelonLoaderGameDir, or MelonLoaderGameDir in Config.Build.user.props (default: the game folder).
#>
param(
    [string]$MelonLoaderGameDir
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$version = (Select-Xml -Path (Join-Path $root "src\OverTheHillUnlimited.props") -XPath "//Version").Node.InnerText
$artifacts = Join-Path $root "artifacts"
New-Item -ItemType Directory -Force $artifacts | Out-Null

function Build([string]$project, [string[]]$extraArgs) {
    dotnet build (Join-Path $root $project) -c Release -p:DeployToGame=false @extraArgs
    if ($LASTEXITCODE -ne 0) { throw "Build of $project failed with exit code $LASTEXITCODE" }
}

# Entry names are written explicitly with forward slashes: Windows PowerShell's ZipFile helpers use backslashes, which
# extract as literal file names on Linux (Proton players)
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
function Pack([string]$zipName, [string]$buildDir, [string[]]$files, [string]$folderInZip) {
    $zip = Join-Path $artifacts $zipName
    if (Test-Path $zip) { Remove-Item -Force $zip }
    $archive = [System.IO.Compression.ZipFile]::Open($zip, [System.IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($file in $files) {
            [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
                $archive, (Join-Path $buildDir $file), "$folderInZip/$file") | Out-Null
        }
    }
    finally {
        $archive.Dispose()
    }
    Write-Host "Packaged $zip"
}

$melonArgs = @()
if ($MelonLoaderGameDir) {
    # A quoted folder ending in a backslash ("C:\Games\over the hill\") arrives through powershell -File with a stray
    # quote, the backslash having escaped it
    $dir = (Resolve-Path $MelonLoaderGameDir.Trim('"')).Path.TrimEnd('\', '/')
    # Passed on without a trailing backslash for the same reason: Windows PowerShell quotes arguments with spaces
    $melonArgs += "-p:MelonLoaderGameDir=$dir"
}

# Both are built before either is packaged, so a failed build can't leave a release half made
Build "src\OverTheHillUnlimited\OverTheHillUnlimited.csproj" @()
Build "src\OverTheHillUnlimited.MelonLoader\OverTheHillUnlimited.MelonLoader.csproj" $melonArgs

Pack "OverTheHillUnlimited-$version.zip" (Join-Path $root "src\OverTheHillUnlimited\bin\Release\net6.0") `
    @("OverTheHillUnlimited.dll", "OverTheHillUnlimited.pdb") "BepInEx/plugins/OverTheHillUnlimited"
Pack "OverTheHillUnlimited-$version-MelonLoader.zip" (Join-Path $root "src\OverTheHillUnlimited.MelonLoader\bin\Release\net6.0") `
    @("OverTheHillUnlimited.MelonLoader.dll", "OverTheHillUnlimited.MelonLoader.pdb") "Mods"
