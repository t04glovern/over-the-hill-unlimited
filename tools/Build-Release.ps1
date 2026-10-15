<#
.SYNOPSIS
    Builds the mod in Release and packages it as artifacts/OverTheHillUnlimited-<version>.zip.

.DESCRIPTION
    The zip mirrors the game folder (BepInEx/plugins/OverTheHillUnlimited/), so players can extract it
    straight into the folder that contains "over the hill.exe". The version comes from <Version> in the csproj.
#>
$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$project = Join-Path $root "src\OverTheHillUnlimited\OverTheHillUnlimited.csproj"
$version = (Select-Xml -Path $project -XPath "//Version").Node.InnerText

dotnet build $project -c Release -p:DeployToGame=false
if ($LASTEXITCODE -ne 0) { throw "Build failed with exit code $LASTEXITCODE" }

$buildDir = Join-Path $root "src\OverTheHillUnlimited\bin\Release\net6.0"
$artifacts = Join-Path $root "artifacts"
$zip = Join-Path $artifacts "OverTheHillUnlimited-$version.zip"

New-Item -ItemType Directory -Force $artifacts | Out-Null
if (Test-Path $zip) { Remove-Item -Force $zip }

# Entry names are written explicitly with forward slashes: Windows PowerShell's ZipFile helpers use
# backslashes, which extract as literal file names on Linux (Proton players)
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::Open($zip, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($file in "OverTheHillUnlimited.dll", "OverTheHillUnlimited.pdb") {
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
            $archive, (Join-Path $buildDir $file), "BepInEx/plugins/OverTheHillUnlimited/$file") | Out-Null
    }
}
finally {
    $archive.Dispose()
}

Write-Host "Packaged $zip"
