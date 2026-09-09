param(
    [Parameter(Mandatory = $true)]
    [string]$Output
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$dir = Split-Path -Parent $Output
if ($dir) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }

function New-IconPng([int]$size) {
    $bitmap = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $graphics.Clear([System.Drawing.Color]::Transparent)

        $scale = $size / 64.0
        $sage = [System.Drawing.Color]::FromArgb(255, 73, 112, 101)
        $sageSoft = [System.Drawing.Color]::FromArgb(255, 112, 146, 135)
        $sand = [System.Drawing.Color]::FromArgb(255, 211, 183, 130)
        $halo = [System.Drawing.Color]::FromArgb(82, 255, 255, 255)

        $haloPen = New-Object System.Drawing.Pen($halo, ([Math]::Max(1.3, 4.7 * $scale)))
        $stemPen = New-Object System.Drawing.Pen($sage, ([Math]::Max(1.2, 3.2 * $scale)))
        $stemPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
        $stemPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
        $haloPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
        $haloPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round

        $stemPath = New-Object System.Drawing.Drawing2D.GraphicsPath
        $stemPath.AddBezier(32*$scale, 52*$scale, 31*$scale, 42*$scale, 32*$scale, 31*$scale, 34*$scale, 17*$scale)
        $graphics.DrawPath($haloPen, $stemPath)
        $graphics.DrawPath($stemPen, $stemPath)

        $left = New-Object System.Drawing.Drawing2D.GraphicsPath
        $left.AddBezier(31*$scale, 35*$scale, 20*$scale, 35*$scale, 13*$scale, 27*$scale, 12*$scale, 16*$scale)
        $left.AddBezier(12*$scale, 16*$scale, 24*$scale, 15*$scale, 33*$scale, 22*$scale, 31*$scale, 35*$scale)
        $left.CloseFigure()

        $right = New-Object System.Drawing.Drawing2D.GraphicsPath
        $right.AddBezier(34*$scale, 30*$scale, 36*$scale, 19*$scale, 45*$scale, 12*$scale, 56*$scale, 13*$scale)
        $right.AddBezier(56*$scale, 13*$scale, 55*$scale, 24*$scale, 47*$scale, 32*$scale, 34*$scale, 30*$scale)
        $right.CloseFigure()

        $leftBrush = New-Object System.Drawing.SolidBrush($sageSoft)
        $rightBrush = New-Object System.Drawing.SolidBrush($sand)
        $graphics.FillPath($leftBrush, $left)
        $graphics.FillPath($rightBrush, $right)

        $leftBrush.Dispose(); $rightBrush.Dispose()
        $left.Dispose(); $right.Dispose(); $stemPath.Dispose()
        $haloPen.Dispose(); $stemPen.Dispose()

        $stream = New-Object System.IO.MemoryStream
        $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
        return ,$stream.ToArray()
    }
    finally {
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}

$sizes = @(16, 20, 24, 32, 40, 48, 64, 128, 256)
$images = @($sizes | ForEach-Object { New-IconPng $_ })

$stream = [System.IO.File]::Open($Output, [System.IO.FileMode]::Create, [System.IO.FileAccess]::Write)
$writer = New-Object System.IO.BinaryWriter($stream)
try {
    $writer.Write([UInt16]0)
    $writer.Write([UInt16]1)
    $writer.Write([UInt16]$images.Count)

    $offset = 6 + (16 * $images.Count)
    for ($i = 0; $i -lt $images.Count; $i++) {
        $size = $sizes[$i]
        $png = $images[$i]
        $dimension = if ($size -eq 256) { 0 } else { $size }
        $writer.Write([Byte]$dimension)
        $writer.Write([Byte]$dimension)
        $writer.Write([Byte]0)
        $writer.Write([Byte]0)
        $writer.Write([UInt16]1)
        $writer.Write([UInt16]32)
        $writer.Write([UInt32]$png.Length)
        $writer.Write([UInt32]$offset)
        $offset += $png.Length
    }

    foreach ($png in $images) { $writer.Write($png) }
}
finally {
    $writer.Dispose()
    $stream.Dispose()
}

Write-Host "Generated $Output"
