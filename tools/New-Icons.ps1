# Draws the Display Profiles icons: a monitor outline with a small "profiles" badge (two stacked cards) at the bottom right,
# in the style of the Windows "Display settings" icon.
#   src/DisplayProfiles.App/Assets/{DisplayProfiles,tray-light,tray-dark}.ico
#   src/DisplayProfiles.Widget/Assets/*.png   (Game Bar widget package assets)
param(
    [string]$AppAssets = (Join-Path $PSScriptRoot '..\src\DisplayProfiles.App\Assets'),
    [string]$WidgetAssets = (Join-Path $PSScriptRoot '..\src\DisplayProfiles.Widget\Assets'),
    [string]$PreviewDir
)

Add-Type -AssemblyName System.Drawing
$ErrorActionPreference = 'Stop'

function New-RoundedRect([double]$x, [double]$y, [double]$w, [double]$h, [double]$r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = [Math]::Min($r * 2, [Math]::Min($w, $h))
    $p.AddArc($x, $y, $d, $d, 180, 90); $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90); $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    return $p
}

# Styles:
#   color      - gray monitor, accent badge, transparent background (app icon)
#   mono       - single-colour monitor and badge with the cards cut out (tray)
#   tile       - white monitor and badge on an accent tile (Game Bar widget)
function New-IconBitmap([int]$size, [string]$style, [System.Drawing.Color]$mono = [System.Drawing.Color]::White, [int]$width = $size, [int]$height = $size, [double]$scale = 1.0) {
    $ss = 4                                   # supersample, then shrink for smooth edges
    $S = [double]($size * $ss)
    $big = New-Object System.Drawing.Bitmap ([int]($width * $ss)), ([int]($height * $ss)), ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($big)
    $g.SmoothingMode = 'AntiAlias'
    $g.Clear([System.Drawing.Color]::Transparent)

    $accent = [System.Drawing.Color]::FromArgb(255, 0, 120, 212)
    $clear = [System.Drawing.Color]::Transparent
    switch ($style) {
        'color' { $line = [System.Drawing.Color]::FromArgb(255, 94, 94, 94); $fill = [System.Drawing.Color]::FromArgb(255, 247, 247, 247); $badge = $accent; $glyph = [System.Drawing.Color]::White; $halo = $clear }
        'mono'  { $line = $mono; $fill = $clear; $badge = $mono; $glyph = $clear; $halo = $clear }
        'tile'  { $line = [System.Drawing.Color]::White; $fill = $clear; $badge = [System.Drawing.Color]::White; $glyph = $accent; $halo = $accent
                  $g.Clear($accent) }
    }

    # Everything is laid out on a size x size square centred in the bitmap.
    $S *= $scale
    $ox = ($width * $ss - $S) / 2; $oy = ($height * $ss - $S) / 2
    function P([double]$u) { return $u * $S }
    $strokeWidth = [Math]::Max(1.15 * $ss, 0.072 * $S)
    $pen = New-Object System.Drawing.Pen $line, $strokeWidth
    $pen.StartCap = 'Round'; $pen.EndCap = 'Round'; $pen.LineJoin = 'Round'
    $g.TranslateTransform($ox, $oy)

    # Screen.
    $screen = New-RoundedRect (P 0.07) (P 0.12) (P 0.86) (P 0.58) (P 0.07)
    if ($fill.A -gt 0) { $g.FillPath((New-Object System.Drawing.SolidBrush $fill), $screen) }
    $g.DrawPath($pen, $screen)
    # Stand.
    $g.DrawLine($pen, (P 0.5), (P 0.70), (P 0.5), (P 0.84))
    $g.DrawLine($pen, (P 0.30), (P 0.865), (P 0.70), (P 0.865))

    # Badge, with a gap around it so it sits in front of the monitor.
    $cx = P 0.735; $cy = P 0.715; $r = P 0.235; $gap = P 0.05
    if ($halo.A -gt 0) {
        $g.FillEllipse((New-Object System.Drawing.SolidBrush $halo), ($cx - $r - $gap), ($cy - $r - $gap), 2 * ($r + $gap), 2 * ($r + $gap))
    } else {
        $g.CompositingMode = 'SourceCopy'
        $g.FillEllipse([System.Drawing.Brushes]::Transparent, ($cx - $r - $gap), ($cy - $r - $gap), 2 * ($r + $gap), 2 * ($r + $gap))
        $g.CompositingMode = 'SourceOver'
    }
    $g.FillEllipse((New-Object System.Drawing.SolidBrush $badge), ($cx - $r), ($cy - $r), 2 * $r, 2 * $r)

    # Two stacked cards = a set of profiles.
    $cw = $r * 0.95; $ch = $r * 0.66; $cr = $r * 0.14
    $back = New-RoundedRect ($cx - $cw / 2 - $r * 0.14) ($cy - $ch / 2 - $r * 0.24) $cw $ch $cr
    $front = New-RoundedRect ($cx - $cw / 2 + $r * 0.14) ($cy - $ch / 2 + $r * 0.17) $cw $ch $cr
    if ($glyph.A -gt 0) {
        $g.FillPath((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(140, $glyph))), $back)
        $g.FillPath((New-Object System.Drawing.SolidBrush $badge), $front.Clone())   # gap between the cards
        $ring = New-Object System.Drawing.Pen $badge, ($r * 0.14)
        $g.DrawPath($ring, $front)
        $g.FillPath((New-Object System.Drawing.SolidBrush $glyph), $front)
    } else {
        $g.CompositingMode = 'SourceCopy'
        $cut = New-Object System.Drawing.Pen ([System.Drawing.Color]::Transparent), ($r * 0.13)
        $g.DrawPath($cut, $back)
        $g.FillPath([System.Drawing.Brushes]::Transparent, $front)
        $g.CompositingMode = 'SourceOver'
    }
    $g.Dispose()

    $small = New-Object System.Drawing.Bitmap $width, $height, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $sg = [System.Drawing.Graphics]::FromImage($small)
    $sg.InterpolationMode = 'HighQualityBicubic'; $sg.PixelOffsetMode = 'HighQuality'; $sg.CompositingQuality = 'HighQuality'
    $sg.DrawImage($big, 0, 0, $width, $height)
    $sg.Dispose(); $big.Dispose()
    return $small
}

function Get-Png($bitmap) {
    $ms = New-Object System.IO.MemoryStream
    $bitmap.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    return , $ms.ToArray()
}

function Save-Ico([string]$path, [int[]]$sizes, [string]$style, [System.Drawing.Color]$mono) {
    $images = foreach ($s in $sizes) { $b = New-IconBitmap $s $style $mono; , (Get-Png $b); $b.Dispose() }
    $fs = [System.IO.File]::Create($path)
    $w = New-Object System.IO.BinaryWriter $fs
    $w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$sizes.Count)
    $offset = 6 + 16 * $sizes.Count
    for ($i = 0; $i -lt $sizes.Count; $i++) {
        $dim = if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] }
        $w.Write([byte]$dim); $w.Write([byte]$dim); $w.Write([byte]0); $w.Write([byte]0)
        $w.Write([uint16]1); $w.Write([uint16]32); $w.Write([uint32]$images[$i].Length); $w.Write([uint32]$offset)
        $offset += $images[$i].Length
    }
    foreach ($img in $images) { $w.Write($img) }
    $w.Dispose(); $fs.Dispose()
}

New-Item -ItemType Directory -Force $AppAssets, $WidgetAssets | Out-Null
$light = [System.Drawing.Color]::FromArgb(255, 26, 26, 26)
Save-Ico (Join-Path $AppAssets 'DisplayProfiles.ico') @(16, 20, 24, 32, 40, 48, 64, 128, 256) 'color' ([System.Drawing.Color]::Black)
Save-Ico (Join-Path $AppAssets 'tray-light.ico') @(16, 20, 24, 32, 48) 'mono' $light
Save-Ico (Join-Path $AppAssets 'tray-dark.ico') @(16, 20, 24, 32, 48) 'mono' ([System.Drawing.Color]::White)
$titleBar = New-IconBitmap 32 'color'
[System.IO.File]::WriteAllBytes((Join-Path $AppAssets 'titlebar.png'), (Get-Png $titleBar)); $titleBar.Dispose()

foreach ($t in @(@('Square150x150Logo.png', 150), @('StoreLogo.png', 50))) {
    $b = New-IconBitmap $t[1] 'tile' ([System.Drawing.Color]::White) $t[1] $t[1] 0.86
    [System.IO.File]::WriteAllBytes((Join-Path $WidgetAssets $t[0]), (Get-Png $b)); $b.Dispose()
}

# Square44x44Logo.png itself is what Game Bar draws on its toolbar, so it is the white glyph on transparent.
# Start, the taskbar and app lists use the target-size files, which keep the accent tile.
$b = New-IconBitmap 44 'mono' ([System.Drawing.Color]::White) 44 44 0.92
[System.IO.File]::WriteAllBytes((Join-Path $WidgetAssets 'Square44x44Logo.png'), (Get-Png $b)); $b.Dispose()
foreach ($size in 16, 24, 32, 48, 256) {
    $b = New-IconBitmap $size 'tile' ([System.Drawing.Color]::White) $size $size 0.86
    [System.IO.File]::WriteAllBytes((Join-Path $WidgetAssets "Square44x44Logo.targetsize-$size.png"), (Get-Png $b)); $b.Dispose()
    $b = New-IconBitmap $size 'color'
    [System.IO.File]::WriteAllBytes((Join-Path $WidgetAssets "Square44x44Logo.targetsize-${size}_altform-unplated.png"), (Get-Png $b)); $b.Dispose()
}
$splash = New-IconBitmap 300 'tile' ([System.Drawing.Color]::White) 620 300 0.7
[System.IO.File]::WriteAllBytes((Join-Path $WidgetAssets 'SplashScreen.png'), (Get-Png $splash)); $splash.Dispose()

# Game Bar draws widget icons as plain glyphs on its dark toolbar, so the toolbar icon is white on transparent.
# The header inside the widget picks the glyph that matches the widget theme.
foreach ($t in @(@('WidgetIcon.png', 88, [System.Drawing.Color]::White), @('HeaderIcon-dark.png', 64, [System.Drawing.Color]::White), @('HeaderIcon-light.png', 64, $light))) {
    $b = New-IconBitmap $t[1] 'mono' $t[2] $t[1] $t[1] 0.92
    [System.IO.File]::WriteAllBytes((Join-Path $WidgetAssets $t[0]), (Get-Png $b)); $b.Dispose()
}

if ($PreviewDir) {
    New-Item -ItemType Directory -Force $PreviewDir | Out-Null
    foreach ($style in 'color', 'tile') { $b = New-IconBitmap 256 $style; $b.Save((Join-Path $PreviewDir "preview-$style.png")); $b.Dispose() }
    $b = New-IconBitmap 256 'mono' $light; $b.Save((Join-Path $PreviewDir 'preview-mono.png')); $b.Dispose()
    foreach ($s in 16, 24, 32) { $b = New-IconBitmap $s 'color'; $b.Save((Join-Path $PreviewDir "small-$s.png")); $b.Dispose() }
}
Write-Host 'Icons written.'
