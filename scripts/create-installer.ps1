# AutoDaily 安装包生成脚本
# 使用方法：在 Windows PowerShell 中运行此脚本
# 需要：Visual Studio 或 MSBuild

param(
    [string]$Version = "1.0.0",
    [string]$OutputDir = ".\dist"
)

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "AutoDaily 安装包生成工具" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

# 检查 MSBuild
$msbuild = "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe"
if (-not (Test-Path $msbuild)) {
    $msbuild = "C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe"
    if (-not (Test-Path $msbuild)) {
        $msbuild = "C:\Program Files (x86)\Microsoft Visual Studio\2017\Community\MSBuild\15.0\Bin\MSBuild.exe"
        if (-not (Test-Path $msbuild)) {
            Write-Host "错误：找不到 MSBuild，请安装 Visual Studio" -ForegroundColor Red
            exit 1
        }
    }
}

Write-Host "`n[1/4] 清理旧文件..." -ForegroundColor Yellow
if (Test-Path $OutputDir) {
    Remove-Item $OutputDir -Recurse -Force
}
New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null

Write-Host "[2/4] 编译项目..." -ForegroundColor Yellow
& $msbuild "AutoDaily.sln" /p:Configuration=Release /p:Platform="Any CPU" /t:Clean,Build /v:minimal
if ($LASTEXITCODE -ne 0) {
    Write-Host "编译失败！" -ForegroundColor Red
    exit 1
}

Write-Host "[3/4] 打包文件..." -ForegroundColor Yellow
$exePath = "AutoDaily\bin\Release\AutoDaily.exe"
if (-not (Test-Path $exePath)) {
    Write-Host "错误：找不到编译输出文件" -ForegroundColor Red
    exit 1
}

# 创建发布目录结构
$packageDir = Join-Path $OutputDir "AutoDaily-v$Version"
New-Item -ItemType Directory -Path $packageDir -Force | Out-Null

# 复制文件
Copy-Item $exePath -Destination $packageDir
Copy-Item "README.md" -Destination $packageDir -ErrorAction SilentlyContinue
Copy-Item "编译说明.md" -Destination $packageDir -ErrorAction SilentlyContinue

# 创建启动说明
$readme = @"
# AutoDaily 日报助手 v$Version

## 安装说明

1. 将整个文件夹复制到您想要的位置（如 C:\Program Files\AutoDaily）
2. 双击 AutoDaily.exe 运行

## 首次使用

1. 点击"🔴 录制"按钮
2. 操作您的目标应用程序
3. 完成后点击"⏹ 完成并保存"
4. 点击"▶️ 运行"测试

## 系统要求

- Windows 7/10/11
- .NET Framework 4.7.2 或更高版本

## 注意事项

- 首次运行可能需要管理员权限（用于全局热键）
- 建议将 AutoDaily.exe 添加到开机启动项

---
编译时间：$(Get-Date -Format "yyyy-MM-dd HH:mm:ss")
"@
$readme | Out-File -FilePath (Join-Path $packageDir "使用说明.txt") -Encoding UTF8

Write-Host "[4/4] 创建压缩包..." -ForegroundColor Yellow
$zipPath = Join-Path $OutputDir "AutoDaily-v$Version.zip"
if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}
Compress-Archive -Path "$packageDir\*" -DestinationPath $zipPath -Force

# 显示结果
$exeSize = (Get-Item $exePath).Length / 1MB
$zipSize = (Get-Item $zipPath).Length / 1MB

Write-Host "`n========================================" -ForegroundColor Green
Write-Host "✓ 打包完成！" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host "EXE 大小: $([math]::Round($exeSize, 2)) MB" -ForegroundColor Cyan
Write-Host "ZIP 大小: $([math]::Round($zipSize, 2)) MB" -ForegroundColor Cyan
Write-Host "`n输出位置: $zipPath" -ForegroundColor Yellow
Write-Host "`n提示：可以将 ZIP 文件分发给用户，解压后即可使用" -ForegroundColor Gray

