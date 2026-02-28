; -- AIchivist Inno Setup Installer Script ──────────────────────────────────
; Bundles the self-contained .NET app + pre-built SQLite database.
; Requires: Inno Setup 6 (https://jrsoftware.org/isinfo.php)

[Setup]
AppName=AIchivist
AppVersion=1.0.0
AppPublisher=Washington State University Libraries
AppSupportURL=https://libraries.wsu.edu
DefaultDirName={autopf}\AIchivist
DefaultGroupName=AIchivist
OutputDir=output
OutputBaseFilename=AIchivist-Setup-1.0.0
Compression=lzma2
SolidCompression=yes
PrivilegesRequired=lowest
WizardStyle=modern
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
DisableProgramGroupPage=yes

; Uncomment and set these when you have icons:
; SetupIconFile=icons\aichivist.ico
; UninstallDisplayIcon={app}\AIchivist.exe

[Types]
Name: "full"; Description: "Full installation (recommended)"
Name: "custom"; Description: "Custom installation"; Flags: iscustom

[Components]
Name: "main"; Description: "AIchivist Application"; Types: full custom; Flags: fixed

[Files]
; Application files (self-contained .NET publish output including wwwroot)
Source: "publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs; Components: main

; Pre-built SQLite database
Source: "data\archive.db"; DestDir: "{app}\data"; Flags: ignoreversion; Components: main

[Icons]
Name: "{group}\AIchivist"; Filename: "{app}\AIchivist.exe"; WorkingDir: "{app}"
Name: "{group}\Uninstall AIchivist"; Filename: "{uninstallexe}"
Name: "{commondesktop}\AIchivist"; Filename: "{app}\AIchivist.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"

[Run]
; Optionally launch the app after install
Filename: "{app}\AIchivist.exe"; \
    Description: "Launch AIchivist"; \
    WorkingDir: "{app}"; \
    Flags: nowait postinstall skipifsilent

[Code]
// Prompt user about data directory removal during uninstall
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  DataDir: String;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    DataDir := ExpandConstant('{localappdata}\AIchivist');
    if DirExists(DataDir) then
    begin
      if MsgBox('Do you want to remove AIchivist settings and logs?'#13#10 +
                'Click Yes to remove all configuration, or No to keep it for future use.',
                mbConfirmation, MB_YESNO) = IDYES then
      begin
        DelTree(DataDir, True, True, True);
      end;
    end;
  end;
end;
