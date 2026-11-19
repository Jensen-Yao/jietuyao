# 从高清母版重制 app.ico（7 尺寸，全部 PNG 帧高质量缩放）与 app.png(256)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$master = [System.Drawing.Image]::FromFile('F:\SelfSoftware\jietuyao\ico.jpg')
$sizes = 16, 24, 32, 48, 64, 128, 256

function Render-Png([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    $g.DrawImage($master, 0, 0, $size, $size)
    $g.Dispose()
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    return ,$ms.ToArray()
}

$frames = @()
foreach ($s in $sizes) { $frames += ,@(Render-Png $s) }

# ICO 组装：ICONDIR + ICONDIRENTRY* + PNG 数据（Vista+ 通用）
$ms = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter($ms)
$bw.Write([UInt16]0); $bw.Write([UInt16]1); $bw.Write([UInt16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]; $b = $frames[$i]
    $dim = if ($s -ge 256) { 0 } else { $s }
    $bw.Write([Byte]$dim); $bw.Write([Byte]$dim)
    $bw.Write([Byte]0); $bw.Write([Byte]0)
    $bw.Write([UInt16]1); $bw.Write([UInt16]32)
    $bw.Write([UInt32]$b.Length); $bw.Write([UInt32]$offset)
    $offset += $b.Length
}
foreach ($b in $frames) { $bw.Write($b) }
$bw.Flush()
[System.IO.File]::WriteAllBytes('F:\SelfSoftware\jietuyao-v2\src\STranslate\Resources\app.ico', $ms.ToArray())

# app.png 256
$png256 = Render-Png 256
[System.IO.File]::WriteAllBytes('F:\SelfSoftware\jietuyao-v2\src\STranslate\Images\app.png', $png256)
# 官网/仓库 logo 同步
[System.IO.File]::WriteAllBytes('F:\SelfSoftware\jietuyao-v2\images\logo.png', $png256)
[System.IO.File]::WriteAllBytes('F:\SelfSoftware\jietuyao-v2\site\logo.png', $png256)

$master.Dispose()
$bw.Dispose()
Write-Output ("app.ico bytes: " + (Get-Item 'F:\SelfSoftware\jietuyao-v2\src\STranslate\Resources\app.ico').Length)
