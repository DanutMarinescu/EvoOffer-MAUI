; Compiled by the Windows installer build script. Requires Inno Setup 6.3 or newer.
; All payload files must come from the same self-contained Windows publish.

#ifndef PublishDir
  #error PublishDir must point to the Windows publish directory.
#endif
#ifndef OutputDir
  #error OutputDir must point to the installer output directory.
#endif
#ifndef AppVersion
  #error AppVersion must be supplied by the build script.
#endif
#ifndef AppName
  #error AppName must be supplied by the build script.
#endif
#ifndef Architecture
  #error Architecture must be x64 or arm64.
#endif
#ifndef OutputBaseFilename
  #error OutputBaseFilename must be supplied by the build script.
#endif
#ifndef WebView2BootstrapperPath
  #error WebView2BootstrapperPath must point to the verified Microsoft bootstrapper.
#endif

#if Architecture == "x64"
  #define AllowedArchitecture "x64compatible"
#elif Architecture == "arm64"
  #define AllowedArchitecture "arm64"
#else
  #error Architecture must be x64 or arm64.
#endif

#if !FileExists(AddBackslash(PublishDir) + "EvoOffer.exe")
  #error PublishDir does not contain EvoOffer.exe.
#endif
#if !FileExists(WebView2BootstrapperPath)
  #error WebView2BootstrapperPath does not exist.
#endif

; Romanian is an optional Inno Setup translation. English is always available.
#if FileExists(AddBackslash(CompilerPath) + "Languages\Romanian.isl")
  #define IncludeRomanian
#endif

[Setup]
; Keep this ID unchanged for all future versions and both architectures.
AppId={{8A4F3437-704B-4DA6-9B50-13AA50293F26}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
VersionInfoVersion={#AppVersion}
VersionInfoDescription={#AppName} Setup
DefaultDirName={localappdata}\Programs\EvoOffer
DefaultGroupName={#AppName}
DisableDirPage=yes
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed={#AllowedArchitecture}
ArchitecturesInstallIn64BitMode={#AllowedArchitecture}
MinVersion=10.0.17763
OutputDir={#OutputDir}
OutputBaseFilename={#OutputBaseFilename}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
SetupLogging=yes
SetupMutex=EvoOfferInstaller
CloseApplications=yes
RestartApplications=no
UninstallDisplayIcon={app}\EvoOffer.exe
UninstallDisplayName={#AppName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
#ifdef IncludeRomanian
Name: "romanian"; MessagesFile: "compiler:Languages\Romanian.isl"
#endif

[CustomMessages]
english.NewerVersionInstalled=A newer version of this application (%1) is already installed. Uninstall it before installing an older version. Your saved settings and documents will be kept.
english.DifferentArchitectureInstalled=The %1 edition of this application is already installed. Uninstall it before installing the %2 edition. Your saved settings and documents will be kept.
english.InstallingWebView2=Installing Microsoft Edge WebView2 for PDF previews. This may take a few minutes and requires an internet connection.
english.WebView2SetupFailed=Microsoft Edge WebView2 could not be installed (code %1). Connect to the internet and try again, or install the Evergreen WebView2 Runtime from https://developer.microsoft.com/microsoft-edge/webview2/ and run this installer again.
english.WebView2RestartRequired=Microsoft Edge WebView2 requires a restart. Restart Windows, then run this installer again.
#ifdef IncludeRomanian
romanian.NewerVersionInstalled=O versiune mai nouă a aplicației (%1) este deja instalată. Dezinstalați-o înainte de a instala o versiune mai veche. Setările și documentele salvate vor fi păstrate.
romanian.DifferentArchitectureInstalled=Ediția %1 a aplicației este deja instalată. Dezinstalați-o înainte de a instala ediția %2. Setările și documentele salvate vor fi păstrate.
romanian.InstallingWebView2=Se instalează Microsoft Edge WebView2 pentru previzualizarea PDF. Operațiunea poate dura câteva minute și necesită o conexiune la internet.
romanian.WebView2SetupFailed=Microsoft Edge WebView2 nu a putut fi instalat (cod %1). Conectați-vă la internet și încercați din nou sau instalați Evergreen WebView2 Runtime de la https://developer.microsoft.com/microsoft-edge/webview2/ și reluați instalarea.
romanian.WebView2RestartRequired=Microsoft Edge WebView2 necesită o repornire. Reporniți Windows, apoi reluați instalarea.
#endif

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; Keep the prerequisite first so temporary extraction avoids the large payload.
Source: "{#WebView2BootstrapperPath}"; DestName: "MicrosoftEdgeWebview2Setup.exe"; Flags: dontcopy noencryption
; Replace the complete published runtime, including files with equal versions.
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\EvoOffer.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\EvoOffer.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\EvoOffer.exe"; WorkingDir: "{app}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

; No blanket cleanup of {app} or AppData: uninstall removes only the installed
; files and shortcuts recorded by Inno Setup, preserving user-created files.

[Code]
function HasWebView2Version(RootKey: Integer): Boolean;
var
  VersionText: String;
  Version: Int64;
begin
  Result := False;
  if RegQueryStringValue(RootKey,
    'Software\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}',
    'pv', VersionText) then
  begin
    if StrToVersion(VersionText, Version) then
      Result := ComparePackedVersion(Version, 0) > 0;
  end;
end;

function IsWebView2Installed: Boolean;
begin
  { Microsoft documents the 32-bit HKLM view and the current user's key:
    https://learn.microsoft.com/microsoft-edge/webview2/concepts/distribution }
  Result := HasWebView2Version(HKLM32) or HasWebView2Version(HKCU);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
begin
  Result := '';
  if IsWebView2Installed then
    Exit;

  WizardForm.PreparingLabel.Caption := CustomMessage('InstallingWebView2');
  Log('WebView2 Runtime is missing; running the bundled Microsoft bootstrapper.');
  ExtractTemporaryFile('MicrosoftEdgeWebview2Setup.exe');
  if not Exec(ExpandConstant('{tmp}\MicrosoftEdgeWebview2Setup.exe'),
    '/silent /install', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
  begin
    Result := FmtMessage(CustomMessage('WebView2SetupFailed'), [IntToStr(ResultCode)]);
    Exit;
  end;

  Log(Format('WebView2 bootstrapper returned %d.', [ResultCode]));
  if ResultCode = 3010 then
  begin
    NeedsRestart := True;
    Result := CustomMessage('WebView2RestartRequired');
    Exit;
  end;

  if (ResultCode <> 0) or not IsWebView2Installed then
    Result := FmtMessage(CustomMessage('WebView2SetupFailed'), [IntToStr(ResultCode)]);
end;

function InitializeSetup: Boolean;
var
  InstalledVersionText, InstalledArchitecture: String;
  InstalledVersion, NewVersion: Int64;
begin
  Result := False;
  InstalledArchitecture := GetPreviousData('Architecture', '');
  if (InstalledArchitecture <> '') and
     (InstalledArchitecture <> '{#Architecture}') then
  begin
    SuppressibleMsgBox(
      FmtMessage(CustomMessage('DifferentArchitectureInstalled'), [InstalledArchitecture, '{#Architecture}']),
      mbError, MB_OK, IDOK);
    Exit;
  end;

  InstalledVersionText := GetPreviousData('AppVersion', '');
  if StrToVersion(InstalledVersionText, InstalledVersion) and
     StrToVersion('{#AppVersion}', NewVersion) then
  begin
    if ComparePackedVersion(InstalledVersion, NewVersion) > 0 then
    begin
      SuppressibleMsgBox(
        FmtMessage(CustomMessage('NewerVersionInstalled'), [InstalledVersionText]),
        mbError, MB_OK, IDOK);
      Exit;
    end;
  end;
  Result := True;
end;

procedure RegisterPreviousData(PreviousDataKey: Integer);
begin
  SetPreviousData(PreviousDataKey, 'Architecture', '{#Architecture}');
  SetPreviousData(PreviousDataKey, 'AppVersion', '{#AppVersion}');
end;
