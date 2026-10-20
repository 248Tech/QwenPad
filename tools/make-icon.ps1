# Renders the QwenPad icon (page + sparkle) at several sizes and writes a PNG-compressed .ico.
param([string]$Out = (Join-Path $PSScriptRoot '..\src\QwenPad.ico'))
Add-Type -AssemblyName System.Drawing

function Render([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.ScaleTransform($size / 32.0, $size / 32.0)
    $g.FillRectangle([System.Drawing.Brushes]::White, 5, 2, 20, 27)
    $border = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(70, 90, 120)), 1
    $g.DrawRectangle($border, 5, 2, 20, 27)
    $line = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(150, 165, 185)), 2
    foreach ($y in 8, 12, 16, 20) { $g.DrawLine($line, 9, $y, 21, $y) }
    $spark = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(124, 77, 255))
    $cx = 24; $cy = 24
    $pts = @(
        [System.Drawing.PointF]::new($cx, $cy - 8), [System.Drawing.PointF]::new($cx + 2.2, $cy - 2.2),
        [System.Drawing.PointF]::new($cx + 8, $cy), [System.Drawing.PointF]::new($cx + 2.2, $cy + 2.2),
        [System.Drawing.PointF]::new($cx, $cy + 8), [System.Drawing.PointF]::new($cx - 2.2, $cy + 2.2),
        [System.Drawing.PointF]::new($cx - 8, $cy), [System.Drawing.PointF]::new($cx - 2.2, $cy - 2.2))
    $g.FillPolygon($spark, $pts)
    $g.Dispose()
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    , $ms.ToArray()
}

$sizes = 16, 24, 32, 48, 64, 256
$images = foreach ($s in $sizes) { , (Render $s) }
$fs = [System.IO.File]::Create($Out)
$w = New-Object System.IO.BinaryWriter $fs
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]; $len = $images[$i].Length
    $w.Write([byte]($s % 256)); $w.Write([byte]($s % 256)); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([uint16]1); $w.Write([uint16]32); $w.Write([uint32]$len); $w.Write([uint32]$offset)
    $offset += $len
}
foreach ($img in $images) { $w.Write($img) }
$w.Close()
Write-Host "Wrote $Out"
