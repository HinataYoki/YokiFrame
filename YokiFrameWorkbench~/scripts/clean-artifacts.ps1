param(
    [switch] $IncludeDefault,
    [switch] $WhatIf
)

# 清理 Workbench 可再生构建缓存。
# 默认只删除历史命名缓存 `.artifacts-*` / `.validation-artifacts*`；
# 加 -IncludeDefault 时一并删除标准 `.artifacts`（下次 build/test 会重建）。

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$targets = @()

Get-ChildItem -Path $root -Directory -Force |
    Where-Object {
        $_.Name -like ".artifacts-*" -or
        $_.Name -like ".validation-artifacts*" -or
        ($IncludeDefault -and $_.Name -eq ".artifacts")
    } |
    ForEach-Object { $targets += $_.FullName }

if ($targets.Count -eq 0) {
    Write-Host "No artifact directories matched under:"
    Write-Host "  $root"
    Write-Host "Default kept: .artifacts (use -IncludeDefault to remove it)."
    exit 0
}

Write-Host "Artifact directories to remove:"
foreach ($path in $targets) {
    Write-Host "  $path"
}

if ($WhatIf) {
    Write-Host "WhatIf: no directories deleted."
    exit 0
}

foreach ($path in $targets) {
    Remove-Item -LiteralPath $path -Recurse -Force
    Write-Host "Removed: $path"
}

Write-Host ("Done. Removed {0} director(y/ies)." -f $targets.Count)
exit 0
