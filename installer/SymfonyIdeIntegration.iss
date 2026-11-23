#define AppName "Symfony IDE Integration"
#define AppExe "SymfonyIdeIntegration.exe"
#define AppVersion GetFileProductVersionString(PublishDir + "\" + AppExe)
#define Scheme "Q3a-Symfony2ide"
#define ProtocolKey "Software\Classes\" + Scheme
#define ProtocolOwner "SymfonyIdeIntegration.Q3a-Symfony2ide"

[Setup]
AppId=SymfonyIdeIntegration.Q3a-Symfony2ide
AppName={#AppName}
LicenseFile=..\LICENSE
AppVersion={#AppVersion}
VersionInfoVersion={#AppVersion}
VersionInfoTextVersion={#AppVersion}
VersionInfoProductVersion={#AppVersion}
VersionInfoProductTextVersion={#AppVersion}
AppMutex={#Scheme}.Tray
DefaultDirName={localappdata}\Programs\SymfonyIdeIntegration
DefaultGroupName={#AppName}
DisableDirPage=yes
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
MinVersion=10.0.22000
ArchitecturesAllowed=x64os
ArchitecturesInstallIn64BitMode=x64os
OutputBaseFilename=SymfonyIdeIntegration-{#AppVersion}-Setup-x64
UninstallDisplayIcon={app}\{#AppExe}
Compression=lzma2
SolidCompression=yes
ChangesAssociations=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "startup"; Description: "{cm:LaunchAtLogon}"

[CustomMessages]
LaunchAtLogon=Start the application when I sign in to Windows
ProtocolInUse=The {#Scheme} protocol already belongs to another application. Setup has stopped.

[Files]
Source: "{#PublishDir}\{#AppExe}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"

[Registry]
Root: HKCU; Subkey: "{#ProtocolKey}"; ValueType: string; ValueName: "Owner"; ValueData: "{#ProtocolOwner}"
Root: HKCU; Subkey: "{#ProtocolKey}"; ValueType: string; ValueData: "URL:{#AppName} Protocol"
Root: HKCU; Subkey: "{#ProtocolKey}"; ValueType: string; ValueName: "URL Protocol"; ValueData: ""
Root: HKCU; Subkey: "{#ProtocolKey}\DefaultIcon"; ValueType: string; ValueData: """{app}\{#AppExe}"",0"
Root: HKCU; Subkey: "{#ProtocolKey}\shell\open\command"; ValueType: string; ValueData: """{app}\{#AppExe}"" --open ""%1"""
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "{#ProtocolOwner}"; ValueData: """{app}\{#AppExe}"""; Tasks: startup; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: none; ValueName: "{#ProtocolOwner}"; Tasks: not startup; Flags: deletevalue

[Run]
Filename: "{app}\{#AppExe}"; Flags: nowait runasoriginaluser

[Code]
function ProtocolIsOwnedByApp: Boolean;
var
  Owner, Command: String;
begin
  Result :=
    RegQueryStringValue(HKCU, '{#ProtocolKey}', 'Owner', Owner) and
    (Owner = '{#ProtocolOwner}') and
    RegQueryStringValue(HKCU, '{#ProtocolKey}\shell\open\command', '', Command) and
    (CompareText(Command, '"' + ExpandConstant('{app}\{#AppExe}') + '" --open "%1"') = 0);
end;

function InitializeSetup: Boolean;
var
  Owner: String;
begin
  Result :=
    not RegKeyExists(HKLM32, '{#ProtocolKey}') and
    not RegKeyExists(HKLM64, '{#ProtocolKey}') and
    (not RegKeyExists(HKCU, '{#ProtocolKey}') or
      (RegQueryStringValue(HKCU, '{#ProtocolKey}', 'Owner', Owner) and
       (Owner = '{#ProtocolOwner}')));
  if not Result then
    SuppressibleMsgBox(CustomMessage('ProtocolInUse'), mbError, MB_OK, IDOK);
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if (CurUninstallStep = usUninstall) and ProtocolIsOwnedByApp then
    RegDeleteKeyIncludingSubkeys(HKCU, '{#ProtocolKey}');
end;
