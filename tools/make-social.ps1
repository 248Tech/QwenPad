# Renders docs/images/icon-256.png and the 1280x640 social preview card from the icon and the dark screenshot.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$root = Split-Path $PSScriptRoot -Parent
$img = Join-Path $root 'docs\images'

$icon = New-Object System.Drawing.Icon (Join-Path $root 'src\QwenPad.ico'), 256, 256
$iconBmp = $icon.ToBitmap()
$iconBmp.Save((Join-Path $img 'icon-256.png'), [System.Drawing.Imaging.ImageFormat]::Png)

$W = 1280; $H = 640
$card = New-Object System.Drawing.Bitmap $W, $H
$g = [System.Drawing.Graphics]::FromImage($card)
$g.SmoothingMode = 'AntiAlias'; $g.TextRenderingHint = 'AntiAliasGridFit'; $g.InterpolationMode = 'HighQualityBicubic'
$bg = New-Object System.Drawing.Drawing2D.LinearGradientBrush ([System.Drawing.Point]::new(0, 0)), ([System.Drawing.Point]::new($W, $H)), ([System.Drawing.ColorTranslator]::FromHtml('#15182e')), ([System.Drawing.ColorTranslator]::FromHtml('#3b1f55'))
$g.FillRectangle($bg, 0, 0, $W, $H)

$g.DrawImage($iconBmp, 72, 78, 112, 112)
$white = [System.Drawing.Brushes]::White
$soft = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(205, 210, 230))
$accent = New-Object System.Drawing.SolidBrush ([System.Drawing.ColorTranslator]::FromHtml('#b39dff'))
$g.DrawString('QwenPad', (New-Object System.Drawing.Font 'Segoe UI Semibold', 64), $white, 200, 82)
$g.DrawString('A tiny, private Notepad', (New-Object System.Drawing.Font 'Segoe UI', 26), $soft, 76, 214)
$g.DrawString('with a local AI assistant', (New-Object System.Drawing.Font 'Segoe UI', 26), $soft, 76, 254)
$y = 330
foreach ($line in '✦  Rewrite · fix · summarize · continue', '✦  100% offline: Qwen3.5 + llama.cpp', '✦  Sticky notes · dark mode · portable') {
    $g.DrawString($line, (New-Object System.Drawing.Font 'Segoe UI', 21), $accent, 80, $y); $y += 46
}
$g.DrawString('github.com/248Tech/QwenPad  ·  Windows 10/11  ·  MIT', (New-Object System.Drawing.Font 'Segoe UI', 17), $soft, 80, 560)

# Screenshot, angled off the right edge.
$shot = [System.Drawing.Image]::FromFile((Join-Path $img 'qwenpad-dark.png'))
$scale = 0.62
$sw = [int]($shot.Width * $scale); $sh = [int]($shot.Height * $scale)
$state = $g.Save()
$g.TranslateTransform(690, 120); $g.RotateTransform(-4)
for ($i = 18; $i -ge 1; $i--) { $sb = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(10, 0, 0, 0)); $g.FillRectangle($sb, -$i + 10, -$i + 16, $sw + 2 * $i, $sh + 2 * $i); $sb.Dispose() }
$g.DrawImage($shot, 0, 0, $sw, $sh)
$g.Restore($state)
$shot.Dispose()

$g.Dispose()
$card.Save((Join-Path $img 'social-preview.png'), [System.Drawing.Imaging.ImageFormat]::Png)
Write-Host "Wrote icon-256.png and social-preview.png to $img"
