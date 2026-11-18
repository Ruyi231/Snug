#ifndef AppVersion
  #define AppVersion "1.1.0"
#endif
#ifndef RootDir
  #define RootDir ".."
#endif
#ifndef AppDir
  #define AppDir "..\dist\stage\self"
#endif
#ifndef DriverDir
  #define DriverDir "..\dist\stage\driver"
#endif
#ifndef OutputDir
  #define OutputDir "..\dist"
#endif

[Setup]
AppId={{8D4F6A2E-7C91-4B3D-9E2A-4D7E1A8F3C55}
AppName=SnugWin
AppVersion={#AppVersion}
AppVerName=SnugWin {#AppVersion}
AppPublisher=nicocat
AppPublisherURL=https://github.com/Ruyi231/Snug
AppSupportURL=https://github.com/Ruyi231/Snug/issues
AppUpdatesURL=https://github.com/Ruyi231/Snug/releases
DefaultDirName={autopf}\SnugWin
DefaultGroupName=SnugWin
AllowNoIcons=yes
LicenseFile={#RootDir}\LICENSE
OutputDir={#OutputDir}
OutputBaseFilename=SnugWin-v{#AppVersion}-win-x64-setup
SetupIconFile={#RootDir}\Assets\snugwin.ico
UninstallDisplayIcon={app}\SnugWin.App.exe
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
AppMutex=Local\SnugWin_SingleInstance_Mutex
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"
Name: "installdriver"; Description: "Install or verify virtual-display-rs 0.3.1 virtual display driver (Recommended)"; GroupDescription: "Driver & Startup:"
Name: "autostart"; Description: "Launch SnugWin silently in tray when signing into Windows"; GroupDescription: "Driver & Startup:"; Flags: unchecked

[Files]
Source: "{#AppDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#DriverDir}\*"; DestDir: "{app}\driver"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#RootDir}\THIRD_PARTY_NOTICES.md"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\SnugWin"; Filename: "{app}\SnugWin.App.exe"; WorkingDir: "{app}"
Name: "{group}\Quick Start Guide"; Filename: "{app}\快速开始.txt"
Name: "{group}\{cm:UninstallProgram,SnugWin}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\SnugWin"; Filename: "{app}\SnugWin.App.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\driver\Install-SnugDriver.ps1"" -Action Install"; StatusMsg: "Verifying and installing virtual display driver (0.3.1)..."; Tasks: installdriver; Flags: waituntilterminated
Filename: "schtasks.exe"; Parameters: "/Create /TN ""SnugWinAutoStart"" /SC ONLOGON /RL HIGHEST /TR ""\""{app}\SnugWin.App.exe\"" --minimized"" /F"; StatusMsg: "Registering Windows logon startup task..."; Tasks: autostart; Flags: runhidden waituntilterminated
Filename: "{app}\SnugWin.App.exe"; WorkingDir: "{app}"; Description: "{cm:LaunchProgram,SnugWin}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "schtasks.exe"; Parameters: "/Delete /TN ""SnugWinAutoStart"" /F"; Flags: runhidden waituntilterminated; RunOnceId: "SnugWinRemoveAutoStart"
Filename: "powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\driver\Install-SnugDriver.ps1"" -Action Uninstall"; Flags: runhidden waituntilterminated; RunOnceId: "SnugWinRemoveDriver"
