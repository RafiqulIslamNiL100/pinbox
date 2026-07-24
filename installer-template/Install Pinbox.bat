@echo off
setlocal
set "SRC=%~dp0"
set "DEST=%LOCALAPPDATA%\Pinbox"

echo ============================================
echo   Installing Pinbox...
echo ============================================
echo.

echo Closing Pinbox if it is running...
taskkill /IM Pinbox.exe /F >nul 2>nul
timeout /t 1 >nul

if exist "%DEST%" (
    echo Removing previous version...
    rmdir /s /q "%DEST%"
)

echo Copying files...
mkdir "%DEST%" >nul 2>nul
xcopy "%SRC%*" "%DEST%\" /E /I /Y /Q >nul
if errorlevel 1 goto error

echo Creating Desktop shortcut...
powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "$s = (New-Object -ComObject WScript.Shell).CreateShortcut('%USERPROFILE%\Desktop\Pinbox.lnk'); $s.TargetPath = '%DEST%\Pinbox.exe'; $s.WorkingDirectory = '%DEST%'; $s.IconLocation = '%DEST%\Pinbox.exe,0'; $s.Save()"
if errorlevel 1 goto error

echo Creating Start Menu shortcut...
powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "$sm = [Environment]::GetFolderPath('StartMenu') + '\Programs'; $s = (New-Object -ComObject WScript.Shell).CreateShortcut(\"$sm\Pinbox.lnk\"); $s.TargetPath = '%DEST%\Pinbox.exe'; $s.WorkingDirectory = '%DEST%'; $s.IconLocation = '%DEST%\Pinbox.exe,0'; $s.Save()"
if errorlevel 1 goto error

echo Refreshing icon cache...
ie4uinit.exe -show >nul 2>nul

echo.
echo ============================================
echo   Pinbox is installed! Starting it now...
echo ============================================
echo.
start "" "%DEST%\Pinbox.exe"
timeout /t 3 >nul
exit /b 0

:error
echo.
echo ============================================
echo   Something went wrong during install.
echo   Copy the text above and send it over.
echo ============================================
echo.
pause
exit /b 1
