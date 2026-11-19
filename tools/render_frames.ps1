param()
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$root = 'F:\SelfSoftware\jietuyao-v2'
$dir = Join-Path $root 'tools\icoframes'
New-Item -ItemType Directory -Force -Path $dir | Out-Null
$master = [System.Drawing.Image]::FromFile('F:\SelfSoftware\jietuyao\ico.jpg')

# ===== 大尺寸（64/128/256）：母版高质量缩放 =====
foreach ($s in @(64, 128, 256)) {
    $bmp = New-Object System.Drawing.Bitmap($s, $s)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    $g.DrawImage($master, 0, 0, $s, $s)
    $g.Dispose()
    $bmp.Save((Join-Path $dir "$s.png"), [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
}

# ===== 小尺寸（16/24/32/48）：像素级简化绘制（红绿旗 + 蓝色截图角标） =====
function Draw-Simplified([int]$s, [bool]$brackets) {
    $bmp = New-Object System.Drawing.Bitmap($s, $s)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::None
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
    $g.Clear([System.Drawing.Color]::Transparent)

    $white = [System.Drawing.Brushes]::White
    $red = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(218, 59, 1))    # DA3B01
    $green = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(16, 124, 16)) # 107C10
    $blue = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(0, 120, 212))  # 0078D4
    $bluePen = New-Object System.Drawing.Pen($blue, [single](1 + [Math]::Floor($s / 24.0)))

    # 白色底（小尺寸直接方底，系统层自会呈现圆角）
    $g.FillRectangle($white, 0, 0, $s, $s)

    if ($brackets) {
        # 四角截图角标（蓝）：像素对齐，直接画八条线
        $in = [int][Math]::Round($s * 0.10)
        $len = [int][Math]::Round($s * 0.20)
        $a = $in
        $b = $s - $in
        $c = $in + $len
        $d2 = $s - $in - $len
        $g.DrawLine($bluePen, $a, $c, $a, $a); $g.DrawLine($bluePen, $a, $a, $c, $a)
        $g.DrawLine($bluePen, $d2, $a, $b, $a); $g.DrawLine($bluePen, $b, $a, $b, $c)
        $g.DrawLine($bluePen, $b, $d2, $b, $b); $g.DrawLine($bluePen, $b, $b, $d2, $b)
        $g.DrawLine($bluePen, $c, $b, $a, $b); $g.DrawLine($bluePen, $a, $b, $a, $d2)
    }

    # 红绿燕尾旗（居中，像素对齐）
    $fx0 = [int]([Math]::Round($s * 0.24)); $fx1 = [int]([Math]::Round($s * 0.76))
    $fy0 = [int]([Math]::Round($s * 0.26)); $fy1 = [int]([Math]::Round($s * 0.74))
    $notch = [int]([Math]::Round($s * 0.14))   # 底部中间燕尾深度
    $mid = [int](($fx0 + $fx1) / 2)
    $g.FillRectangle($red, $fx0, $fy0, ($mid - $fx0), ($fy1 - $fy0))
    $g.FillRectangle($green, $mid, $fy0, ($fx1 - $mid), ($fy1 - $fy0))
    # 燕尾缺口（用底色挖三角）
    $top = $fy1 - $notch
    $tri = New-Object System.Drawing.Drawing2D.GraphicsPath
    $tri.AddLine(($mid - $notch), $fy1, $mid, $top)
    $tri.AddLine($mid, $top, ($mid + $notch), $fy1)
    $tri.CloseFigure()
    $g.FillPath($white, $tri)
    $tri.Dispose()

    $red.Dispose(); $green.Dispose(); $blue.Dispose(); $bluePen.Dispose()
    $g.Dispose()
    return $bmp
}

Draw-Simplified 16 $false | ForEach-Object { $_.Save((Join-Path $dir '16.png'), [System.Drawing.Imaging.ImageFormat]::Png); $_.Dispose() }
Draw-Simplified 24 $false | ForEach-Object { $_.Save((Join-Path $dir '24.png'), [System.Drawing.Imaging.ImageFormat]::Png); $_.Dispose() }
Draw-Simplified 32 $true  | ForEach-Object { $_.Save((Join-Path $dir '32.png'), [System.Drawing.Imaging.ImageFormat]::Png); $_.Dispose() }
Draw-Simplified 48 $true  | ForEach-Object { $_.Save((Join-Path $dir '48.png'), [System.Drawing.Imaging.ImageFormat]::Png); $_.Dispose() }

$master.Dispose()
Write-Output 'frames: simplified small + scaled large'
