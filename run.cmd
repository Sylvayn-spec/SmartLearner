@echo off
setlocal
set "ROOT=%~dp0"
set "DOTNET_ROOT=%USERPROFILE%\.dotnet"
set "DOTNET_MULTILEVEL_LOOKUP=0"
if not exist "%ROOT%src\SmartLearner.App\bin\Debug\net8.0-windows\SmartLearner.App.exe" (
  echo [!] 还没构建，先执行 build-and-run.cmd
  pause
  exit /b 1
)
start "" "%ROOT%src\SmartLearner.App\bin\Debug\net8.0-windows\SmartLearner.App.exe"
