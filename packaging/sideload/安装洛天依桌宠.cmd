@echo off
chcp 65001 >nul
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0install-luotianyi-pet.ps1"
set "INSTALL_EXIT=%ERRORLEVEL%"
if not "%INSTALL_EXIT%"=="0" (
  echo.
  echo 安装未完成。请保留本窗口中的错误信息。
  pause
)
exit /b %INSTALL_EXIT%
