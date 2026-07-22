param(
    [string] $Configuration = "Release",
    [Parameter(Mandatory = $true)]
    [string] $ProjectRoot
)

$ErrorActionPreference = "Stop"

& (Join-Path $PSScriptRoot "publish-workbenchruntime.ps1") `
    -Configuration $Configuration `
    -ProjectRoot $ProjectRoot `
    -RuntimeIdentifier "linux-x64"
