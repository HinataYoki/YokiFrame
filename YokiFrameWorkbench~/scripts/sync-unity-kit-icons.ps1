[CmdletBinding()]
param(
    [switch]$Verify,
    [switch]$ApplyImporterFallback
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$CANVAS_SIZE = 64
$SUPERSAMPLE_FACTOR = 4
$ICON_VIEWBOX_SIZE = 24.0
$STROKE_THICKNESS = 1.7

if ([Threading.Thread]::CurrentThread.ApartmentState -ne [Threading.ApartmentState]::STA)
{
    throw "Run this script with powershell.exe -STA so WPF can render the icon bitmaps."
}

Add-Type -AssemblyName PresentationCore
Add-Type -AssemblyName WindowsBase

$workbenchRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$packageRoot = (Resolve-Path (Join-Path $workbenchRoot "..")).Path
$iconResourcesPath = Join-Path $workbenchRoot "src\YokiFrame.Workbench.Avalonia\Resources\Icons.axaml"
$colorResourcesPath = Join-Path $workbenchRoot "src\YokiFrame.Workbench.Avalonia\Resources\Colors.axaml"
$outputDirectory = Join-Path $packageRoot "Core\Adapters\Unity\Editor\Icons"

$iconDefinitions = @(
    [pscustomobject]@{ FileName = "ActionKit"; ResourceKey = "ActionKit"; Filled = $false },
    [pscustomobject]@{ FileName = "Architecture"; ResourceKey = "Framework"; Filled = $false },
    [pscustomobject]@{ FileName = "AudioKit"; ResourceKey = "AudioKit"; Filled = $false },
    [pscustomobject]@{ FileName = "CodeGenKit"; ResourceKey = "CodeGenKit"; Filled = $false },
    [pscustomobject]@{ FileName = "EventKit"; ResourceKey = "EventKit"; Filled = $false },
    [pscustomobject]@{ FileName = "FsmKit"; ResourceKey = "Fsm"; Filled = $true },
    [pscustomobject]@{ FileName = "InspectorKit"; ResourceKey = "InspectorKit"; Filled = $false },
    [pscustomobject]@{ FileName = "LocalizationKit"; ResourceKey = "LocalizationKit"; Filled = $false },
    [pscustomobject]@{ FileName = "LogKit"; ResourceKey = "LogKit"; Filled = $false },
    [pscustomobject]@{ FileName = "PoolKit"; ResourceKey = "PoolKit"; Filled = $false },
    [pscustomobject]@{ FileName = "ResKit"; ResourceKey = "ResKit"; Filled = $false },
    [pscustomobject]@{ FileName = "SaveKit"; ResourceKey = "SaveKit"; Filled = $false },
    [pscustomobject]@{ FileName = "SceneKit"; ResourceKey = "SceneKit"; Filled = $false },
    [pscustomobject]@{ FileName = "SingletonKit"; ResourceKey = "SingletonKit"; Filled = $false },
    [pscustomobject]@{ FileName = "SpatialKit"; ResourceKey = "SpatialKit"; Filled = $true },
    [pscustomobject]@{ FileName = "TableKit"; ResourceKey = "TableKit"; Filled = $false },
    [pscustomobject]@{ FileName = "ToolClass"; ResourceKey = "ToolClass"; Filled = $false },
    [pscustomobject]@{ FileName = "UIKit"; ResourceKey = "UIKit"; Filled = $false }
)

$importerFallbackTargets = @(
    [pscustomobject]@{ RelativePath = "Tools\SceneKit"; IconFileName = "SceneKit" },
    [pscustomobject]@{ RelativePath = "Tools\UIKit"; IconFileName = "UIKit" }
)

function Get-DarkThemeSection
{
    param(
        [string]$Resources
    )

    $startToken = '<ResourceDictionary x:Key="Dark">'
    $start = $Resources.IndexOf($startToken, [StringComparison]::Ordinal)
    if ($start -lt 0)
    {
        throw "Workbench Dark theme resources were not found."
    }

    $end = $Resources.IndexOf("</ResourceDictionary>", $start, [StringComparison]::Ordinal)
    if ($end -lt 0)
    {
        throw "Workbench Dark theme resources are not closed correctly."
    }

    return $Resources.Substring($start, $end - $start)
}

function Get-IconGeometryData
{
    param(
        [string]$Resources,
        [string]$ResourceKey
    )

    $pattern = '<StreamGeometry\s+x:Key="Icon\.Navigation\.' + [Regex]::Escape($ResourceKey) + '">(?<path>[^<]+)</StreamGeometry>'
    $match = [Regex]::Match($Resources, $pattern)
    if (-not $match.Success)
    {
        throw "Workbench icon geometry was not found: Icon.Navigation.$ResourceKey"
    }

    return $match.Groups["path"].Value.Trim()
}

function Get-IconColor
{
    param(
        [string]$ThemeResources,
        [string]$ResourceKey
    )

    $pattern = '<SolidColorBrush\s+x:Key="Brush\.Icon\.' + [Regex]::Escape($ResourceKey) + '"\s+Color="(?<color>#[0-9A-Fa-f]{6})"\s*/>'
    $match = [Regex]::Match($ThemeResources, $pattern)
    if (-not $match.Success)
    {
        throw "Workbench icon color was not found: Brush.Icon.$ResourceKey"
    }

    return $match.Groups["color"].Value
}

function New-IconBitmap
{
    param(
        [string]$GeometryData,
        [string]$ColorValue,
        [bool]$Filled
    )

    $renderSize = $CANVAS_SIZE * $SUPERSAMPLE_FACTOR
    $scale = $renderSize / $ICON_VIEWBOX_SIZE
    $geometry = [System.Windows.Media.Geometry]::Parse($GeometryData)
    $color = [System.Windows.Media.ColorConverter]::ConvertFromString($ColorValue)
    $brush = [System.Windows.Media.SolidColorBrush]::new($color)
    $brush.Freeze()

    $pen = [System.Windows.Media.Pen]::new($brush, $STROKE_THICKNESS)
    $pen.StartLineCap = [System.Windows.Media.PenLineCap]::Round
    $pen.EndLineCap = [System.Windows.Media.PenLineCap]::Round
    $pen.LineJoin = [System.Windows.Media.PenLineJoin]::Round
    $pen.Freeze()

    $visual = [System.Windows.Media.DrawingVisual]::new()
    $context = $visual.RenderOpen()
    try
    {
        $context.PushTransform([System.Windows.Media.ScaleTransform]::new($scale, $scale))
        $fill = if ($Filled) { $brush } else { $null }
        $context.DrawGeometry($fill, $pen, $geometry)
        $context.Pop()
    }
    finally
    {
        $context.Close()
    }

    $highResolution = [System.Windows.Media.Imaging.RenderTargetBitmap]::new(
        $renderSize,
        $renderSize,
        96,
        96,
        [System.Windows.Media.PixelFormats]::Pbgra32)
    $highResolution.Render($visual)
    $bitmap = [System.Windows.Media.Imaging.TransformedBitmap]::new(
        $highResolution,
        [System.Windows.Media.ScaleTransform]::new(1.0 / $SUPERSAMPLE_FACTOR, 1.0 / $SUPERSAMPLE_FACTOR))
    $bitmap.Freeze()
    return $bitmap
}

function Save-Png
{
    param(
        [System.Windows.Media.Imaging.BitmapSource]$Bitmap,
        [string]$Path
    )

    $temporaryPath = $Path + ".tmp"
    $stream = [IO.File]::Open($temporaryPath, [IO.FileMode]::Create, [IO.FileAccess]::Write, [IO.FileShare]::None)
    try
    {
        $encoder = [System.Windows.Media.Imaging.PngBitmapEncoder]::new()
        $encoder.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($Bitmap))
        $encoder.Save($stream)
    }
    finally
    {
        $stream.Dispose()
    }

    Move-Item -LiteralPath $temporaryPath -Destination $Path -Force
}

function Get-BitmapPixels
{
    param(
        [System.Windows.Media.Imaging.BitmapSource]$Bitmap
    )

    $pixelFormat = [System.Windows.Media.PixelFormats]::Pbgra32
    $source = if ($Bitmap.Format -eq $pixelFormat)
    {
        $Bitmap
    }
    else
    {
        [System.Windows.Media.Imaging.FormatConvertedBitmap]::new($Bitmap, $pixelFormat, $null, 0)
    }
    $stride = $source.PixelWidth * 4
    $pixels = [byte[]]::new($stride * $source.PixelHeight)
    $source.CopyPixels($pixels, $stride, 0)
    return ,$pixels
}

function Read-PngBitmap
{
    param(
        [string]$Path
    )

    $uri = [Uri]::new((Resolve-Path $Path).Path)
    $decoder = [System.Windows.Media.Imaging.PngBitmapDecoder]::new(
        $uri,
        [System.Windows.Media.Imaging.BitmapCreateOptions]::PreservePixelFormat,
        [System.Windows.Media.Imaging.BitmapCacheOption]::OnLoad)
    return $decoder.Frames[0]
}

function Convert-ToPngRoundTripBitmap
{
    param(
        [System.Windows.Media.Imaging.BitmapSource]$Bitmap
    )

    $stream = [IO.MemoryStream]::new()
    try
    {
        $encoder = [System.Windows.Media.Imaging.PngBitmapEncoder]::new()
        $encoder.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($Bitmap))
        $encoder.Save($stream)
        $stream.Position = 0
        $decoder = [System.Windows.Media.Imaging.PngBitmapDecoder]::new(
            $stream,
            [System.Windows.Media.Imaging.BitmapCreateOptions]::PreservePixelFormat,
            [System.Windows.Media.Imaging.BitmapCacheOption]::OnLoad)
        return $decoder.Frames[0]
    }
    finally
    {
        $stream.Dispose()
    }
}

function Assert-IconMatchesSource
{
    param(
        [System.Windows.Media.Imaging.BitmapSource]$Expected,
        [string]$Path
    )

    if (-not (Test-Path -LiteralPath $Path))
    {
        throw "Unity icon is missing: $Path"
    }

    $expectedPng = Convert-ToPngRoundTripBitmap -Bitmap $Expected
    $actual = Read-PngBitmap -Path $Path
    if ($actual.PixelWidth -ne $expectedPng.PixelWidth -or $actual.PixelHeight -ne $expectedPng.PixelHeight)
    {
        throw "Unity icon dimensions do not match: $Path"
    }

    $expectedPixels = Get-BitmapPixels -Bitmap $expectedPng
    $actualPixels = Get-BitmapPixels -Bitmap $actual
    for ($index = 0; $index -lt $expectedPixels.Length; $index++)
    {
        if ($expectedPixels[$index] -ne $actualPixels[$index])
        {
            throw "Unity icon pixels are not synchronized with the Workbench source: $Path"
        }
    }
}

function Write-TextAtomically
{
    param(
        [string]$Path,
        [string]$Content
    )

    $temporaryPath = $Path + ".tmp"
    $encoding = [Text.UTF8Encoding]::new($false)
    [IO.File]::WriteAllText($temporaryPath, $Content, $encoding)
    Move-Item -LiteralPath $temporaryPath -Destination $Path -Force
}

function Get-IconGuid
{
    param(
        [string]$IconFileName
    )

    $metaPath = Join-Path $outputDirectory ($IconFileName + ".png.meta")
    $match = [Regex]::Match([IO.File]::ReadAllText($metaPath), '(?m)^guid: (?<guid>[0-9a-f]{32})$')
    if (-not $match.Success)
    {
        throw "Unity icon GUID was not found: $metaPath"
    }

    return $match.Groups["guid"].Value
}

function Set-MonoImporterIcon
{
    param(
        [string]$MetaPath,
        [string]$IconGuid
    )

    $content = [IO.File]::ReadAllText($MetaPath)
    $iconLine = "  icon: {fileID: 2800000, guid: $IconGuid, type: 3}"
    if ($content.IndexOf($iconLine, [StringComparison]::Ordinal) -ge 0)
    {
        return
    }

    $newLine = if ($content.Contains("`r`n")) { "`r`n" } else { "`n" }
    if ($content -match '(?m)^MonoImporter:\r?$')
    {
        if ($content -match '(?m)^  icon: .*\r?$')
        {
            $content = [Regex]::Replace($content, '(?m)^  icon: .*\r?$', $iconLine)
        }
        elseif ($content -match '(?m)^  executionOrder: .*\r?$')
        {
            $content = [Regex]::Replace(
                $content,
                '(?m)^(  executionOrder: .*\r?$)',
                '$1' + $newLine + $iconLine)
        }
        else
        {
            throw "MonoImporter layout is not supported: $MetaPath"
        }
    }
    elseif ($content -match '\AfileFormatVersion: 2\r?\nguid: [0-9a-f]{32}\r?\n?\z')
    {
        $content = $content.TrimEnd("`r", "`n") + $newLine +
            "MonoImporter:" + $newLine +
            "  externalObjects: {}" + $newLine +
            "  serializedVersion: 2" + $newLine +
            "  defaultReferences: []" + $newLine +
            "  executionOrder: 0" + $newLine +
            $iconLine + $newLine +
            "  userData: " + $newLine +
            "  assetBundleName: " + $newLine +
            "  assetBundleVariant: " + $newLine
    }
    else
    {
        throw "Script meta layout is not supported: $MetaPath"
    }

    Write-TextAtomically -Path $MetaPath -Content $content
}

function Apply-MonoImporterFallback
{
    param(
        [pscustomobject]$Target
    )

    $targetRoot = Join-Path $packageRoot $Target.RelativePath
    $iconGuid = Get-IconGuid -IconFileName $Target.IconFileName
    $scriptMetaFiles = Get-ChildItem -Path $targetRoot -Recurse -File -Filter "*.cs.meta" |
        Where-Object { $_.FullName -notmatch '[\\/]+Tests[\\/]+' }
    foreach ($scriptMeta in $scriptMetaFiles)
    {
        Set-MonoImporterIcon -MetaPath $scriptMeta.FullName -IconGuid $iconGuid
    }

    Write-Output "Applied MonoImporter fallback: $($Target.IconFileName) ($($scriptMetaFiles.Count) scripts)"
}

function Assert-MonoImporterIcons
{
    param(
        [pscustomobject]$Target
    )

    $targetRoot = Join-Path $packageRoot $Target.RelativePath
    $iconGuid = Get-IconGuid -IconFileName $Target.IconFileName
    $expectedIcon = "icon: {fileID: 2800000, guid: $iconGuid, type: 3}"
    $scriptMetaFiles = Get-ChildItem -Path $targetRoot -Recurse -File -Filter "*.cs.meta" |
        Where-Object { $_.FullName -notmatch '[\\/]+Tests[\\/]+' }
    if ($scriptMetaFiles.Count -eq 0)
    {
        throw "No script meta files were found: $targetRoot"
    }

    foreach ($scriptMeta in $scriptMetaFiles)
    {
        if ([IO.File]::ReadAllText($scriptMeta.FullName).IndexOf($expectedIcon, [StringComparison]::Ordinal) -lt 0)
        {
            throw "Script icon fallback was not applied: $($scriptMeta.FullName)"
        }
    }
}

$iconResources = [IO.File]::ReadAllText($iconResourcesPath)
$darkThemeResources = Get-DarkThemeSection -Resources ([IO.File]::ReadAllText($colorResourcesPath))
foreach ($icon in $iconDefinitions)
{
    $geometryData = Get-IconGeometryData -Resources $iconResources -ResourceKey $icon.ResourceKey
    $colorValue = Get-IconColor -ThemeResources $darkThemeResources -ResourceKey $icon.ResourceKey
    $bitmap = New-IconBitmap -GeometryData $geometryData -ColorValue $colorValue -Filled $icon.Filled
    $outputPath = Join-Path $outputDirectory ($icon.FileName + ".png")
    if ($Verify)
    {
        Assert-IconMatchesSource -Expected $bitmap -Path $outputPath
        continue
    }

    Save-Png -Bitmap $bitmap -Path $outputPath
    Write-Output "Synchronized Unity icon: $($icon.FileName).png"
}

if ($ApplyImporterFallback)
{
    foreach ($target in $importerFallbackTargets)
    {
        Apply-MonoImporterFallback -Target $target
    }
}

if ($Verify)
{
    foreach ($target in $importerFallbackTargets)
    {
        Assert-MonoImporterIcons -Target $target
    }

    Write-Output "Unity Kit icons match the Workbench Dark navigation resources."
}
