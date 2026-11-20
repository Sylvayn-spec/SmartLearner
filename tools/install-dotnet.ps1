# 安装 .NET 8 SDK 到用户目录（免管理员）
# 用法：powershell -ExecutionPolicy Bypass -File tools\install-dotnet.ps1

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$installDir = Join-Path $env:USERPROFILE '.dotnet'
$scriptPath = Join-Path $env:TEMP 'dotnet-install.ps1'

if (Test-Path (Join-Path $installDir 'dotnet.exe')) {
    $existing = & (Join-Path $installDir 'dotnet.exe') --list-sdks
    if ($existing -match '^8\.') {
        Write-Host "[*] 已检测到 .NET 8 SDK，跳过安装。" -ForegroundColor Green
        $existing
        exit 0
    }
}

Write-Host "[*] 下载 dotnet-install.ps1 ..."
Invoke-WebRequest -Uri 'https://dot.net/v1/dotnet-install.ps1' -OutFile $scriptPath -UseBasicParsing

Write-Host "[*] 安装 .NET 8 SDK 到 $installDir ..."
& $scriptPath -Channel 8.0 -InstallDir $installDir -NoPath

Write-Host "[*] 安装完成，已安装的 SDK：" -ForegroundColor Green
& (Join-Path $installDir 'dotnet.exe') --list-sdks
