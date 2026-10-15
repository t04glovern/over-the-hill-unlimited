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
$staging = Join-Path $artifacts "staging"
$pluginDir = Join-Path $staging "BepInEx\plugins\OverTheHillUnlimited"
$zip = Join-Path $artifacts "OverTheHillUnlimited-$version.zip"

if (Test-Path $staging) { Remove-Item -Recurse -Force $staging }
New-Item -ItemType Directory -Force $pluginDir | Out-Null
Copy-Item (Join-Path $buildDir "OverTheHillUnlimited.dll"), (Join-Path $buildDir "OverTheHillUnlimited.pdb") $pluginDir

# ZipFile writes forward-slash entry names, unlike Compress-Archive on Windows PowerShell,
# so the zip also extracts correctly on Linux (Proton players)
if (Test-Path $zip) { Remove-Item -Force $zip }
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory($staging, $zip)
Remove-Item -Recurse -Force $staging

Write-Host "Packaged $zip"
