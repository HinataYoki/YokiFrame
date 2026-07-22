param(
    [string] $Configuration = "Release",
    [Parameter(Mandatory = $true)]
    [string] $ProjectRoot,
    [Parameter(Mandatory = $true)]
    [ValidateSet("win-x64", "win-x64-aot", "linux-x64", "osx-x64", "osx-arm64")]
    [string] $RuntimeIdentifier,
    [switch] $StartupOptimized
)

$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$workbenchRoot = Resolve-Path (Join-Path $scriptRoot "..")
$packageRoot = Resolve-Path (Join-Path $workbenchRoot "..")
$packagingProjectPath = Join-Path $workbenchRoot "src\YokiFrame.Packaging\YokiFrame.Packaging.csproj"
$arguments = @(
    "run",
    "--project",
    $packagingProjectPath,
    "--",
    "runtime",
    "publish",
    "--package-root",
    $packageRoot,
    "--project-root",
    $ProjectRoot,
    "--configuration",
    $Configuration,
    "--profile",
    $RuntimeIdentifier
)

if ($StartupOptimized) {
    $arguments += "--startup-optimized"
}

& dotnet @arguments
$exitCode = $LASTEXITCODE
if ($exitCode -ne 0) {
    exit $exitCode
}

Write-Host "YokiFrame project Runtime cache publish completed for $RuntimeIdentifier."
exit 0
