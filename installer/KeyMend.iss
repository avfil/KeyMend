#define MyAppName "KeyMend"
#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif
#ifndef VersionInfoVersion
  #define VersionInfoVersion "1.0.0.0"
#endif
#ifndef PublishDir
  #define PublishDir "..\artifacts\publish"
#endif
#ifndef ArtifactsDir
  #define ArtifactsDir "..\artifacts\installer"
#endif

[Setup]
AppId={{E8C8061F-AE3E-4A58-8F14-6E78A0C9F0C1}
AppName={#MyAppName}
AppVersion={#AppVersion}
AppVerName={#MyAppName} {#AppVersion}
AppPublisher=avfil
AppPublisherURL=https://github.com/avfil/KeyMend
AppSupportURL=https://github.com/avfil/KeyMend/issues
AppUpdatesURL=https://github.com/avfil/KeyMend/releases
DefaultDirName={localappdata}\Programs\KeyMend
DisableDirPage=yes
DisableProgramGroupPage=yes
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
PrivilegesRequired=lowest
OutputDir={#ArtifactsDir}
OutputBaseFilename=KeyMend-Setup
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
MinVersion=10.0
CloseApplications=yes
CloseApplicationsFilter=KeyMend.exe
AppMutex=Local\KeyMend.SingleInstance
UninstallDisplayIcon={app}\KeyMend.exe
UninstallDisplayName={#MyAppName}
VersionInfoVersion={#VersionInfoVersion}
VersionInfoProductName={#MyAppName}
VersionInfoDescription=KeyMend installer

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
russian.AdminModeWarning=Обнаружен автозапуск KeyMend от администратора. Перед обновлением или удалением выключите этот режим в меню значка в трее, а после обновления включите заново.
english.AdminModeWarning=KeyMend administrator autostart was detected. Turn it off from the tray icon menu before updating or uninstalling, then turn it on again after updating.

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#PublishDir}\KeyMend.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\KeyMend.exe"
Name: "{userdesktop}\{#MyAppName}"; Filename: "{app}\KeyMend.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\KeyMend.exe"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent

[Code]
var
  PreviousAutostart: string;

function SafeUserName(const Value: string): string;
var
  I: Integer;
  C: Char;
begin
  Result := '';
  for I := 1 to Length(Value) do
  begin
    C := Value[I];
    if ((C >= 'A') and (C <= 'Z')) or
       ((C >= 'a') and (C <= 'z')) or
       ((C >= '0') and (C <= '9')) or
       (C = '.') or (C = '-') or (C = '_') then
      Result := Result + C
    else
      Result := Result + '_';
  end;
  if Result = '' then
    Result := 'user';
end;

function AdminTaskPresent: Boolean;
var
  TaskName: string;
  ResultCode: Integer;
begin
  TaskName := 'KeyMend-' + SafeUserName(GetEnv('USERNAME'));
  Result := Exec(ExpandConstant('{cmd}'), '/C schtasks /Query /TN "' + TaskName + '" >NUL 2>NUL',
    '', SW_HIDE, ewWaitUntilTerminated, ResultCode) and (ResultCode = 0);
end;

procedure ShowAdminModeWarning;
begin
  if AdminTaskPresent then
    MsgBox(ExpandConstant('{cm:AdminModeWarning}'), mbInformation, MB_OK);
end;

function AutostartCommand(const ExePath: string): string;
begin
  Result := '"' + ExePath + '"';
end;

function InitializeSetup: Boolean;
begin
  PreviousAutostart := '';
  if not RegQueryStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'KeyMend', PreviousAutostart) then
    PreviousAutostart := '';
  ShowAdminModeWarning;
  Result := True;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if (CurStep = ssPostInstall) and (PreviousAutostart <> '') then
    RegWriteStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'KeyMend',
      AutostartCommand(ExpandConstant('{app}\KeyMend.exe')));
end;

function InitializeUninstall: Boolean;
begin
  ShowAdminModeWarning;
  Result := True;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  CurrentAutostart: string;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    CurrentAutostart := '';
    if RegQueryStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'KeyMend', CurrentAutostart) and
       (CompareText(CurrentAutostart, AutostartCommand(ExpandConstant('{app}\KeyMend.exe'))) = 0) then
      RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'KeyMend');
  end;
end;
