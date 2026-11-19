Add-Type -AssemblyName System.Drawing
foreach ($s in 16, 32) {
    $p = "F:\SelfSoftware\jietuyao-v2\tools\icoframes\$s.png"
    $src = New-Object System.Drawing.Bitmap($p)
    $big = New-Object System.Drawing.Bitmap($src, 256, 256)
    $big.Save("F:\SelfSoftware\jietuyao-v2\tools\icoframes\${s}_x256.png", [System.Drawing.Imaging.ImageFormat]::Png)
    $src.Dispose(); $big.Dispose()
}
Write-Output upscaled
