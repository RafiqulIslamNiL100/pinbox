; Pinbox installer script (NSIS)
; Built with the standalone `makensis` compiler (Ubuntu's `nsis` package),
; not electron-builder's Node wrapper - that matters because the wrapper
; downloads a code-signing toolkit bundling macOS files that fail to
; extract without a Windows privilege some accounts don't have. This
; script needs nothing beyond makensis itself, so that problem doesn't
; apply here.

!include "MUI2.nsh"

Name "Pinbox"
OutFile "..\Pinbox-Setup.exe"
InstallDir "$LOCALAPPDATA\Pinbox"
RequestExecutionLevel user
Unicode true
SetCompressor /SOLID lzma

!define MUI_ICON "..\src\Pinbox\Assets\icon.ico"
!define MUI_UNICON "..\src\Pinbox\Assets\icon.ico"
!define MUI_FINISHPAGE_RUN "$INSTDIR\Pinbox.exe"
!define MUI_FINISHPAGE_RUN_TEXT "Launch Pinbox"

!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES

!insertmacro MUI_LANGUAGE "English"

Section "Install"
  ; Close Pinbox if it's already running, so an update can overwrite its
  ; files. taskkill returns as soon as it's issued the termination request,
  ; but Windows can take a brief moment afterward to actually release the
  ; process's file handles (coreclr.dll, etc.) - copying immediately after
  ; can still hit "file in use" errors on a fresh update, so give it a
  ; moment to actually finish tearing the process down.
  nsExec::Exec 'taskkill /IM Pinbox.exe /F'
  Sleep 1500

  SetOutPath "$INSTDIR"
  File /r "..\publish-output\*.*"

  CreateShortcut "$DESKTOP\Pinbox.lnk" "$INSTDIR\Pinbox.exe"
  CreateDirectory "$SMPROGRAMS\Pinbox"
  CreateShortcut "$SMPROGRAMS\Pinbox\Pinbox.lnk" "$INSTDIR\Pinbox.exe"
  CreateShortcut "$SMPROGRAMS\Pinbox\Uninstall Pinbox.lnk" "$INSTDIR\Uninstall.exe"

  WriteUninstaller "$INSTDIR\Uninstall.exe"

  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\Pinbox" "DisplayName" "Pinbox"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\Pinbox" "UninstallString" "$INSTDIR\Uninstall.exe"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\Pinbox" "InstallLocation" "$INSTDIR"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\Pinbox" "DisplayIcon" "$INSTDIR\Pinbox.exe"
SectionEnd

Section "Uninstall"
  Delete "$DESKTOP\Pinbox.lnk"
  RMDir /r "$SMPROGRAMS\Pinbox"
  RMDir /r "$INSTDIR"
  DeleteRegKey HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\Pinbox"
SectionEnd
