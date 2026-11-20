#define AppName "Smart Learner"
#define AppVersion "1.0.0"
#define AppExe "SmartLearner.App.exe"

[Setup]
AppId={{7D3B0D6A-6C7E-4E1B-9D2A-5A1C8F0E4B21}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=Smart Learner
DefaultDirName={autopf}\Smart Learner
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
OutputDir=dist
OutputBaseFilename=SmartLearner-Setup-{#AppVersion}
SetupIconFile=src\SmartLearner.App\Assets\app.ico
UninstallDisplayIcon={app}\{#AppExe}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
InfoBeforeFile=packaging\before-install.txt
ShowLanguageDialog=auto

[Languages]
Name: "chinesesimplified"; MessagesFile: "packaging\ChineseSimplified.isl"

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "附加任务："; Flags: checkedonce
Name: "quicklaunchicon"; Description: "创建快速启动栏快捷方式"; GroupDescription: "附加任务："; Flags: unchecked

[Files]
Source: "artifacts\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon
Name: "{userappdata}\Microsoft\Internet Explorer\Quick Launch\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: quicklaunchicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "运行 {#AppName}"; Flags: nowait postinstall skipifsilent

[Code]
function WebView2Present(): Boolean;
begin
  Result := DirExists(ExpandConstant('{pf32}\Microsoft\EdgeWebView\Application')) or
            DirExists(ExpandConstant('{pf64}\Microsoft\EdgeWebView\Application'));
end;

function InitializeSetup(): Boolean;
begin
  Result := True;
  if not WebView2Present() then
  begin
    if MsgBox('未检测到 Microsoft Edge WebView2 运行时。' + #13#10 +
              'PDF 预览、Markdown 预览和整理结果需要它才能显示。' + #13#10 + #13#10 +
              '可以继续安装，但建议先安装 WebView2 运行时。是否继续？',
              mbConfirmation, MB_YESNO) = IDNO then
    begin
      Result := False;
    end;
  end;
end;
