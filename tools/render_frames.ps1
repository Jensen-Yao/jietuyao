param()
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$root = 'F:\SelfSoftware\jietuyao-v2'
$dir = Join-Path $root 'tools\icoframes'
New-Item -ItemType Directory -Force -Path $dir | Out-Null

$master = [System.Drawing.Image]::FromFile('F:\SelfSoftware\jietuyao\ico.jpg')
foreach ($s in @(16, 24, 32, 48, 64, 128, 256)) {
    $bmp = New-Object System.Drawing.Bitmap($s, $s)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    $g.DrawImage($master, 0, 0, $s, $s)
    $g.Dispose()
    $out = Join-Path $dir "$s.png"
    $bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Output "rendered $out"
}
$master.Dispose()
