; -*- coding: utf-8 -*-
Unicode true
!include "MUI2.nsh"
!include "nsDialogs.nsh"
!include "LogicLib.nsh"
!include "FileFunc.nsh"
!include "x64.nsh"
!include "WinVer.nsh"

Name "轻截 ${APP_VERSION}"
OutFile "${OUTPUT_FILE}"
RequestExecutionLevel user
SetCompressor /FINAL zlib
SetDatablockOptimize off
ShowInstDetails nevershow
ShowUninstDetails nevershow
VIProductVersion "${APP_VERSION}.0"
VIAddVersionKey /LANG=2052 "ProductName" "LightCapture"
VIAddVersionKey /LANG=2052 "FileDescription" "轻截安装程序"
VIAddVersionKey /LANG=2052 "FileVersion" "${APP_VERSION}"
VIAddVersionKey /LANG=2052 "LegalCopyright" "LightCapture contributors"
!define MUI_ICON "${APP_ICON}"
!define MUI_UNICON "${APP_ICON}"
!define MUI_FINISHPAGE_TITLE "轻截已安装"
!define MUI_FINISHPAGE_TEXT "从开始菜单打开轻截即可使用。$\r$\n$\r$\n若旧版仍在运行，请先从托盘退出旧版，再打开新版。"
Page custom OptionsPage OptionsLeave
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "SimpChinese"

; 测试构建替换全部外部位置，不能写真实启动项、卸载项或桌面。
!ifdef TEST_ROOT
  !define APP_KEY "Software\LightCapture\InstallerTests\${TEST_ID}\Uninstall"
  !define RUN_KEY "Software\LightCapture\InstallerTests\${TEST_ID}\Run"
!else
  !define APP_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\LightCapture"
  !define RUN_KEY "Software\Microsoft\Windows\CurrentVersion\Run"
!endif

Var AutoStart
Var DesktopLink
Var AutoCheck
Var DesktopCheck
Var MenuDir
Var DesktopDir
Var Dialog
Var OwnDesktopLink

; 保存本安装创建的快捷方式原始内容；用户改过或替换过的同名文件不属于本安装。
!macro DesktopOwnership Prefix
Function ${Prefix}CheckDesktopLink
  StrCpy $OwnDesktopLink 0
  ClearErrors
  FileOpen $1 "$INSTDIR\LightCapture.DesktopShortcut.dat" r
  ${If} ${Errors}
    Return
  ${EndIf}
  FileOpen $2 "$DesktopDir\轻截.lnk" r
  ${If} ${Errors}
    FileClose $1
    Return
  ${EndIf}
  FileSeek $1 0 END $3
  FileSeek $2 0 END $4
  ${If} $3 == $4
  ${AndIf} $3 > 0
    FileSeek $1 0 SET
    FileSeek $2 0 SET
    StrCpy $OwnDesktopLink 1
    ${DoWhile} $3 > 0
      ClearErrors
      FileReadByte $1 $4
      FileReadByte $2 $5
      ${If} ${Errors}
      ${OrIf} $4 != $5
        StrCpy $OwnDesktopLink 0
        ${ExitDo}
      ${EndIf}
      IntOp $3 $3 - 1
    ${Loop}
  ${EndIf}
  FileClose $1
  FileClose $2
FunctionEnd
!macroend
!insertmacro DesktopOwnership ""
!insertmacro DesktopOwnership "un."

!macro DeleteInstalledFile RelativePath
  ${If} ${FileExists} "$INSTDIR\${RelativePath}"
    ClearErrors
    Delete "$INSTDIR\${RelativePath}"
    ${If} ${Errors}
      !insertmacro Fail "部分程序文件无法删除。请关闭占用文件的程序后重试卸载。" 31
    ${EndIf}
  ${EndIf}
!macroend

!macro Fail Message Code
  MessageBox MB_OK|MB_ICONSTOP "${Message}" /SD IDOK
  SetErrorLevel ${Code}
  Abort
!macroend

!macro InitFolders
  SetShellVarContext current
  SetRegView 64
  !ifdef TEST_ROOT
    StrCpy $MenuDir "${TEST_ROOT}\Shortcuts"
    StrCpy $DesktopDir "${TEST_ROOT}\Desktop"
  !else
    StrCpy $MenuDir "$SMPROGRAMS\轻截"
    StrCpy $DesktopDir "$DESKTOP"
  !endif
!macroend

!macro CheckAppClosed
  IfFileExists "$INSTDIR\LightCapture.exe" 0 check_closed_done
  System::Call 'kernel32::CreateFileW(w "$INSTDIR\LightCapture.exe", i 0x40000000, i 1, p 0, i 3, i 0, p 0) p.r0'
  ${If} $0 == -1
    !insertmacro Fail "程序文件正在使用或无法写入。请从托盘退出轻截后重试。" 10
  ${EndIf}
  System::Call 'kernel32::CloseHandle(p r0)'
  check_closed_done:
!macroend

Function .onInit
  ${IfNot} ${RunningX64}
    !insertmacro Fail "需要 Windows 10/11 64 位系统。" 11
  ${EndIf}
  ${IfNot} ${AtLeastWin10}
    !insertmacro Fail "需要 Windows 10 或更新版本。" 11
  ${EndIf}
  !insertmacro InitFolders
  ; 固定按用户安装，不接受 /D 改到系统目录，也不沿用便携包下载路径。
  !ifdef TEST_ROOT
    StrCpy $INSTDIR "${TEST_ROOT}\App"
  !else
    StrCpy $INSTDIR "$LOCALAPPDATA\Programs\LightCapture"
  !endif
  StrCpy $AutoStart 1
  StrCpy $DesktopLink 0
  ReadRegStr $0 HKCU "${APP_KEY}" "InstallLocation"
  ${If} $0 != ""
    ReadRegStr $0 HKCU "${RUN_KEY}" "LightCapture"
    ${If} $0 == ""
      StrCpy $AutoStart 0
    ${EndIf}
  ${EndIf}
  Call CheckDesktopLink
  ${If} $OwnDesktopLink == 1
    StrCpy $DesktopLink 1
  ${EndIf}
  ${GetParameters} $0
  ${GetOptions} $0 "/AUTOSTART=" $1
  ${IfNot} ${Errors}
    ${If} $1 != 0
    ${AndIf} $1 != 1
      !insertmacro Fail "AUTOSTART 参数只能为 0 或 1。" 12
    ${EndIf}
    StrCpy $AutoStart $1
  ${EndIf}
  ClearErrors
  ${GetOptions} $0 "/DESKTOP=" $1
  ${IfNot} ${Errors}
    ${If} $1 != 0
    ${AndIf} $1 != 1
      !insertmacro Fail "DESKTOP 参数只能为 0 或 1。" 12
    ${EndIf}
    StrCpy $DesktopLink $1
  ${EndIf}
FunctionEnd

Function OptionsPage
  !insertmacro MUI_HEADER_TEXT "安装轻截" "截图、贴图、离线翻译与 GIF"
  nsDialogs::Create 1018
  Pop $Dialog
  ${If} $Dialog == error
    Abort
  ${EndIf}
  ${NSD_CreateLabel} 0 0 100% 30u "安装位置：$\r$\n$INSTDIR"
  Pop $0
  ${NSD_CreateCheckbox} 0 44u 100% 14u "开机自启动"
  Pop $AutoCheck
  ${NSD_SetState} $AutoCheck $AutoStart
  ${NSD_CreateCheckbox} 0 66u 100% 14u "创建桌面快捷方式"
  Pop $DesktopCheck
  ${NSD_SetState} $DesktopCheck $DesktopLink
  ${NSD_CreateLabel} 0 100u 100% 30u "仅为当前用户安装，包含离线语言包。$\r$\n更新前请从托盘退出旧版。"
  Pop $0
  nsDialogs::Show
FunctionEnd

Function OptionsLeave
  ${NSD_GetState} $AutoCheck $AutoStart
  ${NSD_GetState} $DesktopCheck $DesktopLink
FunctionEnd

Section "轻截"
  !insertmacro CheckAppClosed
  SetOverwrite on
  ClearErrors
  SetOutPath "$INSTDIR"
  File /r "${PUBLISH_DIR}\*"
  WriteUninstaller "$INSTDIR\Uninstall.exe"
  ${If} ${Errors}
    !insertmacro Fail "安装文件写入失败。请检查磁盘空间与目录权限。" 20
  ${EndIf}
  ClearErrors
  WriteRegStr HKCU "${APP_KEY}" "DisplayName" "轻截 / LightCapture"
  WriteRegStr HKCU "${APP_KEY}" "DisplayVersion" "${APP_VERSION}"
  WriteRegStr HKCU "${APP_KEY}" "Publisher" "LightCapture"
  WriteRegStr HKCU "${APP_KEY}" "InstallLocation" "$INSTDIR"
  WriteRegStr HKCU "${APP_KEY}" "DisplayIcon" '"$INSTDIR\LightCapture.exe",0'
  WriteRegStr HKCU "${APP_KEY}" "UninstallString" '"$INSTDIR\Uninstall.exe"'
  WriteRegStr HKCU "${APP_KEY}" "QuietUninstallString" '"$INSTDIR\Uninstall.exe" /S'
  WriteRegDWORD HKCU "${APP_KEY}" "NoModify" 1
  WriteRegDWORD HKCU "${APP_KEY}" "NoRepair" 1
  WriteRegDWORD HKCU "${APP_KEY}" "EstimatedSize" ${SIZE_KB}
  ${If} ${Errors}
    !insertmacro Fail "卸载信息保存失败，请检查当前账户权限。" 21
  ${EndIf}
  ${If} $AutoStart == 1
    WriteRegStr HKCU "${RUN_KEY}" "LightCapture" '"$INSTDIR\LightCapture.exe" --autostart'
  ${Else}
    ReadRegStr $0 HKCU "${RUN_KEY}" "LightCapture"
    ClearErrors
    ${If} $0 != ""
      DeleteRegValue HKCU "${RUN_KEY}" "LightCapture"
    ${EndIf}
  ${EndIf}
  ${If} ${Errors}
    !insertmacro Fail "启动项或卸载信息保存失败，请检查当前账户权限。" 21
  ${EndIf}
  CreateDirectory "$MenuDir"
  CreateShortCut "$MenuDir\轻截.lnk" "$INSTDIR\LightCapture.exe"
  CreateShortCut "$MenuDir\卸载轻截.lnk" "$INSTDIR\Uninstall.exe"
  Call CheckDesktopLink
  ${If} $DesktopLink == 1
    ${If} ${FileExists} "$DesktopDir\轻截.lnk"
    ${AndIf} $OwnDesktopLink == 0
      MessageBox MB_OK|MB_ICONINFORMATION "桌面已有其他同名快捷方式，已保留。可从开始菜单打开轻截。" /SD IDOK
    ${Else}
      ClearErrors
      CreateDirectory "$DesktopDir"
      CreateShortCut "$DesktopDir\轻截.lnk" "$INSTDIR\LightCapture.exe"
      ${IfNot} ${Errors}
        CopyFiles /SILENT "$DesktopDir\轻截.lnk" "$INSTDIR\LightCapture.DesktopShortcut.dat"
      ${EndIf}
    ${EndIf}
  ${Else}
    ${If} $OwnDesktopLink == 1
      Delete "$DesktopDir\轻截.lnk"
    ${EndIf}
    Delete "$INSTDIR\LightCapture.DesktopShortcut.dat"
  ${EndIf}
  SetErrorLevel 0
SectionEnd

Function un.onInit
  !insertmacro InitFolders
  ReadRegStr $0 HKCU "${APP_KEY}" "InstallLocation"
  ${If} $0 != $INSTDIR
    !insertmacro Fail "安装位置不匹配，请从 Windows 已安装的应用中卸载轻截。" 30
  ${EndIf}
FunctionEnd

Section "Uninstall"
  !insertmacro CheckAppClosed
  ; 清单由发布文件生成，逐一删除，不递归删除用户可能放入的其他文件。
  !include "${GENERATED_DIR}\uninstall-files.nsh"
  Call un.CheckDesktopLink
  ${If} $OwnDesktopLink == 1
    Delete "$DesktopDir\轻截.lnk"
  ${EndIf}
  Delete "$INSTDIR\LightCapture.DesktopShortcut.dat"
  Delete "$INSTDIR\Uninstall.exe"
  RMDir "$INSTDIR"
  ReadRegStr $0 HKCU "${RUN_KEY}" "LightCapture"
  ${If} $0 == '"$INSTDIR\LightCapture.exe" --autostart'
    DeleteRegValue HKCU "${RUN_KEY}" "LightCapture"
  ${EndIf}
  DeleteRegKey HKCU "${APP_KEY}"
  Delete "$MenuDir\轻截.lnk"
  Delete "$MenuDir\卸载轻截.lnk"
  RMDir "$MenuDir"
  SetErrorLevel 0
SectionEnd
