<#
.SYNOPSIS
    Dumps the game's IL2CPP code to .dump/ using Cpp2IL.

.DESCRIPTION
    -Format diffable (default): searchable C# stubs in .dump/diffable/DiffableCs. No method bodies, but every type,
    field, method, Fusion attribute ([Networked], [Capacity(n)], [NetworkedWeaved(offset, words)]) and
    [Calls]/[CalledBy] cross-references, which is what we use to find patch targets.

    -Format isil: each method's machine code as ISIL in .dump/isil/IsilDump (~700 MB), with call targets resolved to
    names. Use it to see what a method actually does: constants, comparisons, which field offsets it reads, and
    bounds checks (a list index check calls ThrowHelper.ThrowArgumentOutOfRange_IndexException).

    Re-run after every game update.
#>
param(
    [ValidateSet("diffable", "isil")]
    [string]$Format = "diffable",
    [string]$GameDir = "C:\Program Files (x86)\Steam\steamapps\common\over the hill",
    [string]$Cpp2ILVersion = "2022.1.0-pre-release.21"
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$binDir = Join-Path $PSScriptRoot "bin"
$exe = Join-Path $binDir "Cpp2IL-$Cpp2ILVersion-Windows.exe"
$outDir = Join-Path $root ".dump\$Format"

if (-not (Test-Path $exe)) {
    New-Item -ItemType Directory -Force $binDir | Out-Null
    $url = "https://github.com/SamboyCoding/Cpp2IL/releases/download/$Cpp2ILVersion/Cpp2IL-$Cpp2ILVersion-Windows.exe"
    Write-Host "Downloading Cpp2IL $Cpp2ILVersion..."
    Invoke-WebRequest $url -OutFile $exe
}

if (Test-Path $outDir) { Remove-Item -Recurse -Force $outDir }

if ($Format -eq "diffable") {
    & $exe --game-path $GameDir --exe-name "over the hill" `
        --use-processor attributeanalyzer,attributeinjector,callanalyzer `
        --output-as diffable-cs --output-to $outDir
}
else {
    & $exe --game-path $GameDir --exe-name "over the hill" --output-as isil --output-to $outDir
}
if ($LASTEXITCODE -ne 0) { throw "Cpp2IL failed with exit code $LASTEXITCODE" }

Write-Host "Dump written to $outDir (game code is in the Assembly-CSharp folder)"
