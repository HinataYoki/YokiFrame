param(
    [string] $Configuration = "Release",
    [Parameter(Mandatory = $true)]
    [string] $ProjectRoot,
    [switch] $StartupOptimized,
    [switch] $NativeAot
)

$ErrorActionPreference = "Stop"

$sharedScript = Join-Path $PSScriptRoot "publish-workbenchruntime.ps1"
if ($NativeAot) {
    & $sharedScript `
        -Configuration $Configuration `
        -ProjectRoot $ProjectRoot `
        -RuntimeIdentifier "win-x64-aot"
    return
}

if ($StartupOptimized) {
    & $sharedScript `
        -Configuration $Configuration `
        -ProjectRoot $ProjectRoot `
        -RuntimeIdentifier "win-x64" `
        -StartupOptimized
    return
}

& $sharedScript `
    -Configuration $Configuration `
    -ProjectRoot $ProjectRoot `
    -RuntimeIdentifier "win-x64"
