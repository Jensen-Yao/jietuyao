# 从命令行参数获取版本号,如果未提供则使用默认值
param(
    [string]$Version = "2.0.0",
    [string]$RepoUrl = "https://github.com/STranslate/STranslate"
)

$ErrorActionPreference = "Stop"

function Log([string]$msg, [string]$color = "Yellow") {
    Write-Host $msg -ForegroundColor $color
}

# 去除版本号中的 'v' 前缀(如果存在)
$CleanVersion = $Version -replace '^v', ''

Log "开始打包 STranslate 版本: $CleanVersion" "Green"

# 检查源目录是否存在
$SourcePath = ".\src\.artifacts\Release\"
if (-not (Test-Path $SourcePath)) {
    Log "错误: 源目录不存在: $SourcePath" "Red"
    exit 1
}

# 检查图标是否存在
$IconPath = ".\src\STranslate\Resources\updater.ico"
if (-not (Test-Path $IconPath)) {
    Log "错误: 图标文件不存在: $IconPath" "Red"
    exit 1
}

# 确保输出目录存在
$OutputPath = ".\publish"
if (-not (Test-Path $OutputPath)) {
    Log "创建输出目录: $OutputPath" "Cyan"
    New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null
}

# 下载最新的 GitHub release 以方便比对生成 delta 包
Log "下载最新的 GitHub release..." "Cyan"
$downloadSuccess = $false
$downloadedFiles = @()

try {
    # 记录下载前的文件
    $filesBefore = Get-ChildItem -Path $OutputPath -File | Select-Object -ExpandProperty Name

    vpk download github --repoUrl $RepoUrl -o $OutputPath

    if ($LASTEXITCODE -eq 0) {
        Log "成功下载最新 release" "Green"
        $downloadSuccess = $true

        # 记录下载后新增的文件
        $filesAfter = Get-ChildItem -Path $OutputPath -File | Select-Object -ExpandProperty Name
        $downloadedFiles = $filesAfter | Where-Object { $_ -notin $filesBefore }

        if ($downloadedFiles.Count -gt 0) {
            Log "已下载文件: $($downloadedFiles -join ', ')" "Cyan"
        }
    } else {
        Log "警告: 下载 release 失败,将跳过 delta 包生成" "Yellow"
    }
} catch {
    Log "警告: 下载 release 时出错: $($_.Exception.Message)" "Yellow"
    Log "将继续进行完整包打包..." "Yellow"
}

# 执行 vpk 打包
Log "执行 vpk 打包..." "Cyan"
vpk pack -u STranslate -v $CleanVersion -p $SourcePath -o $OutputPath -i $IconPath

if ($LASTEXITCODE -ne 0) {
    Log "错误: vpk 打包失败" "Red"
    exit 1
}

Log "打包完成!" "Green"

# 在 STranslate-win-Portable.zip 的 current 目录中添加 PortableConfig 文件夹
Log "处理便携版 zip 文件..." "Cyan"

$PortableZipPath = Join-Path $OutputPath "STranslate-win-Portable.zip"

if (Test-Path $PortableZipPath) {
    try {
        # 加载 .NET 压缩程序集
        Add-Type -AssemblyName System.IO.Compression.FileSystem

        # 以更新模式打开 ZIP 文件
        $zip = [System.IO.Compression.ZipFile]::Open($PortableZipPath, [System.IO.Compression.ZipArchiveMode]::Update)

        try {
            # 在 zip 内创建空目录 entry：current/PortableConfig/
            $entryPath = "current/PortableConfig/"

            # 如果 entry 已存在先删除（避免重复）
            $existing = $zip.GetEntry($entryPath)
            if ($existing) {
                $existing.Delete()
                Log "已删除旧的 PortableConfig 目录" "Gray"
            }

            # 创建目录 entry（目录 entry 必须以 "/" 结尾）
            $zip.CreateEntry($entryPath) | Out-Null
            Log "处理成功「已在 current 目录中添加 PortableConfig 文件夹」" "Green"
        }
        finally {
            $zip.Dispose()
        }
    }
    catch {
        Log "错误: 处理便携版 zip 文件时出错: $($_.Exception.Message)" "Red"
    }
} else {
    Log "警告: 未找到 STranslate-win-Portable.zip 文件，跳过 PortableConfig 添加" "Yellow"
}

# 清理下载的文件
if ($downloadSuccess -and $downloadedFiles.Count -gt 0) {
    Log "清理下载的临时文件..." "Cyan"
    foreach ($file in $downloadedFiles) {
        $filePath = Join-Path $OutputPath $file
        if (Test-Path $filePath) {
            try {
                Remove-Item -Path $filePath -Force
                Log "  已删除: $file" "Gray"
            } catch {
                Log "  警告: 无法删除 $file - $($_.Exception.Message)" "Yellow"
            }
        }
    }
    Log "临时文件清理完成" "Green"
}

# 列出生成的文件
Log "生成的文件:" "Cyan"
Get-ChildItem -Path $OutputPath | ForEach-Object {
    Log "  - $($_.Name)" "White"
}
