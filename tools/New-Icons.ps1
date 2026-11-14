# Generates the app and tray icons from the Segoe Fluent Icons "TVMonitor" glyph (U+E7F4).
# Output: src/DisplayProfiles.App/Assets/{DisplayProfiles,tray-light,tray-dark}.ico
param([string]$OutDir = (Join-Path $PSScriptRoot '..\src\DisplayProfiles.App\Assets'))

Add-Type -AssemblyName System.Drawing
New-Item -ItemType Directory -Force $OutDir | Out-Null
$glyph = [string][char]0xE7F4

function New-GlyphPng([int]$size, [System.Drawing.Color]$foreground, [System.Drawing.Color]$background, [double]$glyphScale) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.TextRenderingHint = 'AntiAliasGridFit'
    $g.Clear([System.Drawing.Color]::Transparent)
    if ($background.A -gt 0) {
        $radius = [Math]::Max(2, $size * 0.22)
        $path = New-Object System.Drawing.Drawing2D.GraphicsPath
        $d = $radius * 2
        $path.AddArc(0, 0, $d, $d, 180, 90); $path.AddArc($size - $d, 0, $d, $d, 270, 90)
        $path.AddArc($size - $d, $size - $d, $d, $d, 0, 90); $path.AddArc(0, $size - $d, $d, $d, 90, 90)
        $path.CloseFigure()
        $g.FillPath((New-Object System.Drawing.SolidBrush $background), $path)
    }
    $font = New-Object System.Drawing.Font 'Segoe Fluent Icons', ([single]($size * $glyphScale)), ([System.Drawing.FontStyle]::Regular), ([System.Drawing.GraphicsUnit]::Pixel)
    $format = New-Object System.Drawing.StringFormat
    $format.Alignment = 'Center'; $format.LineAlignment = 'Center'
    $rect = New-Object System.Drawing.RectangleF 0, 0, $size, $size
    $g.DrawString($glyph, $font, (New-Object System.Drawing.SolidBrush $foreground), $rect, $format)
    $g.Dispose()
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    return , $ms.ToArray()
}

function Save-Ico([string]$path, [int[]]$sizes, [System.Drawing.Color]$foreground, [System.Drawing.Color]$background, [double]$glyphScale) {
    $images = foreach ($s in $sizes) { , (New-GlyphPng $s $foreground $background $glyphScale) }
    $fs = [System.IO.File]::Create($path)
    $w = New-Object System.IO.BinaryWriter $fs
    $w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$sizes.Count)
    $offset = 6 + 16 * $sizes.Count
    for ($i = 0; $i -lt $sizes.Count; $i++) {
        $s = $sizes[$i]
        $dim = if ($s -ge 256) { 0 } else { $s }
        $w.Write([byte]$dim); $w.Write([byte]$dim)
        $w.Write([byte]0); $w.Write([byte]0); $w.Write([uint16]1); $w.Write([uint16]32)
        $w.Write([uint32]$images[$i].Length); $w.Write([uint32]$offset)
        $offset += $images[$i].Length
    }
    foreach ($img in $images) { $w.Write($img) }
    $w.Dispose(); $fs.Dispose()
}

$accent = [System.Drawing.Color]::FromArgb(255, 0, 120, 212)
$clear = [System.Drawing.Color]::FromArgb(0, 0, 0, 0)
Save-Ico (Join-Path $OutDir 'DisplayProfiles.ico') @(16, 24, 32, 48, 64, 128, 256) ([System.Drawing.Color]::White) $accent 0.62
Save-Ico (Join-Path $OutDir 'tray-light.ico') @(16, 20, 24, 32, 48) ([System.Drawing.Color]::FromArgb(255, 26, 26, 26)) $clear 0.95
Save-Ico (Join-Path $OutDir 'tray-dark.ico') @(16, 20, 24, 32, 48) ([System.Drawing.Color]::White) $clear 0.95
Write-Host "Icons written to $OutDir"

# Game Bar widget package assets.
$WidgetDir = Join-Path $PSScriptRoot '..\src\DisplayProfiles.Widget\Assets'
New-Item -ItemType Directory -Force $WidgetDir | Out-Null
[System.IO.File]::WriteAllBytes((Join-Path $WidgetDir 'Square44x44Logo.png'), (New-GlyphPng 44 ([System.Drawing.Color]::White) $accent 0.62))
[System.IO.File]::WriteAllBytes((Join-Path $WidgetDir 'Square150x150Logo.png'), (New-GlyphPng 150 ([System.Drawing.Color]::White) $accent 0.62))
[System.IO.File]::WriteAllBytes((Join-Path $WidgetDir 'StoreLogo.png'), (New-GlyphPng 50 ([System.Drawing.Color]::White) $accent 0.62))

$splash = New-Object System.Drawing.Bitmap 620, 300
$g = [System.Drawing.Graphics]::FromImage($splash)
$g.Clear($accent)
$g.TextRenderingHint = 'AntiAliasGridFit'
$font = New-Object System.Drawing.Font 'Segoe Fluent Icons', 120, ([System.Drawing.FontStyle]::Regular), ([System.Drawing.GraphicsUnit]::Pixel)
$format = New-Object System.Drawing.StringFormat; $format.Alignment = 'Center'; $format.LineAlignment = 'Center'
$g.DrawString($glyph, $font, [System.Drawing.Brushes]::White, (New-Object System.Drawing.RectangleF 0, 0, 620, 300), $format)
$g.Dispose()
$splash.Save((Join-Path $WidgetDir 'SplashScreen.png'), [System.Drawing.Imaging.ImageFormat]::Png)
$splash.Dispose()
Write-Host "Widget assets written to $WidgetDir"
