@echo off
setlocal
set "ROOT=%~dp0"
if not exist "%USERPROFILE%\.dotnet\dotnet.exe" (
  echo [!] 未找到 .NET SDK，请先运行 tools\install-dotnet.ps1
  pause
  exit /b 1
)
set "DOTNET_ROOT=%USERPROFILE%\.dotnet"
set "DOTNET_MULTILEVEL_LOOKUP=0"
"%DOTNET_ROOT%\dotnet.exe" build "%ROOT%SmartLearner.sln" -c Debug
if errorlevel 1 (
  echo [!] 构建失败
  pause
  exit /b 1
)
echo [*] 构建成功，启动 Smart Learner...
start "" "%ROOT%src\SmartLearner.App\bin\Debug\net8.0-windows\SmartLearner.App.exe"
