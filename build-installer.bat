@echo off
setlocal
cd /d "%~dp0"

echo ============================================
echo  Building Pinbox for Windows
echo ============================================
echo.

echo [1/3] Publishing (requires .NET 8 SDK - https://dotnet.microsoft.com/download)...
dotnet publish src\Pinbox\Pinbox.csproj -r win-x64 -c Release --self-contained true -o publish-output
if errorlevel 1 goto error

echo.
echo [2/3] Adding the installer script...
copy /y "installer-template\Install Pinbox.bat" "publish-output\Install Pinbox.bat" >nul
if errorlevel 1 goto error

echo.
echo [3/3] Zipping it up...
if exist Pinbox-for-Windows.zip del Pinbox-for-Windows.zip
powershell -NoProfile -Command "Compress-Archive -Path 'publish-output\*' -DestinationPath 'Pinbox-for-Windows.zip' -Force"
if errorlevel 1 goto error

echo.
echo ============================================
echo  DONE! Pinbox-for-Windows.zip is ready.
echo  Commit and push it to GitHub, or send it
echo  directly - extract + run "Install Pinbox.bat"
echo  installs it.
echo ============================================
echo.
pause
exit /b 0

:error
echo.
echo Something went wrong above. Copy the red text and send it over.
echo.
pause
exit /b 1
