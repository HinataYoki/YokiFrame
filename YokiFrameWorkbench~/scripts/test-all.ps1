param(
    [string] $Configuration = "Release",
    [switch] $NoBuild
)

# 逐项目运行 Workbench solution 测试。
# slnx 上直接 `dotnet test` 在当前 SDK 下可能只构建、不分派测试，因此用本脚本汇总。

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$testProjects = @(
    "tests/YokiFrame.Protocol.Tests/YokiFrame.Protocol.Tests.csproj",
    "tests/YokiFrame.Client.Tests/YokiFrame.Client.Tests.csproj",
    "tests/YokiFrame.Tooling.Application.Tests/YokiFrame.Tooling.Application.Tests.csproj",
    "tests/YokiFrame.Cli.Tests/YokiFrame.Cli.Tests.csproj",
    "tests/YokiFrame.Installer.Core.Tests/YokiFrame.Installer.Core.Tests.csproj",
    "tests/YokiFrame.Packaging.Tests/YokiFrame.Packaging.Tests.csproj",
    "tests/YokiFrame.Workbench.Avalonia.Tests/YokiFrame.Workbench.Avalonia.Tests.csproj",
    "tests/YokiFrame.Godot.Runtime.Tests/YokiFrame.Godot.Runtime.Tests.csproj",
    "tests/YokiFrame.Godot.Editor.Tests/YokiFrame.Godot.Editor.Tests.csproj",
    "tests/YokiFrame.Godot.Player.Tests/YokiFrame.Godot.Player.Tests.csproj"
)

$failed = @()
$passed = @()

foreach ($relative in $testProjects) {
    $projectPath = Join-Path $root $relative
    if (-not (Test-Path $projectPath)) {
        Write-Host "SKIP missing: $relative"
        continue
    }

    Write-Host ""
    Write-Host "=== TEST $relative ==="
    $args = @(
        "test",
        $projectPath,
        "-c", $Configuration,
        "--nologo",
        "--verbosity", "minimal"
    )
    if ($NoBuild) {
        $args += "--no-build"
    }

    & dotnet @args
    if ($LASTEXITCODE -ne 0) {
        $failed += $relative
        Write-Host "FAILED: $relative (exit $LASTEXITCODE)"
    }
    else {
        $passed += $relative
        Write-Host "PASSED: $relative"
    }
}

Write-Host ""
Write-Host "=== SUMMARY ==="
Write-Host ("Passed: {0}" -f $passed.Count)
Write-Host ("Failed: {0}" -f $failed.Count)
if ($failed.Count -gt 0) {
    foreach ($item in $failed) {
        Write-Host "  - $item"
    }
    exit 1
}

exit 0
