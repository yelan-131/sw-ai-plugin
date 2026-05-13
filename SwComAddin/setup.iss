; SW AI Plugin - Inno Setup Installer Script
; Build: ISCC.exe setup.iss

#define AppName "SW AI Plugin"
#define AppVersion "0.1.0"
#define AppPublisher "SW AI Plugin"
#define AppURL "https://github.com/yelan-131/sw-ai-plugin"
#define AppExeName "SwComAddin.dll"

[Setup]
AppId={{B3E7D8A1-4F2C-4A91-B5D6-E8F0A1C2D3E4}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}
DefaultDirName={autopf}\SW AI Plugin
DefaultGroupName=SW AI Plugin
DisableProgramGroupPage=yes
OutputDir=installer
OutputBaseFilename=SwAiPlugin_Setup_v{#AppVersion}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
UninstallDisplayName=SW AI Plugin
SetupIconFile=
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
Name: "chinese"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "附加图标:"; Flags: unchecked

[Files]
; Main DLL
Source: "bin\Debug\net48\SwComAddin.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "bin\Debug\net48\SwComAddin.tlb"; DestDir: "{app}"; Flags: ignoreversion

; SolidWorks Interop
Source: "bin\Debug\net48\SolidWorks.Interop.sldworks.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "bin\Debug\net48\SolidWorks.Interop.swconst.dll"; DestDir: "{app}"; Flags: ignoreversion

; .NET Dependencies
Source: "bin\Debug\net48\Microsoft.Bcl.AsyncInterfaces.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "bin\Debug\net48\System.Buffers.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "bin\Debug\net48\System.Memory.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "bin\Debug\net48\System.Numerics.Vectors.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "bin\Debug\net48\System.Runtime.CompilerServices.Unsafe.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "bin\Debug\net48\System.Text.Encodings.Web.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "bin\Debug\net48\System.Text.Json.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "bin\Debug\net48\System.Threading.Tasks.Extensions.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "bin\Debug\net48\System.ValueTuple.dll"; DestDir: "{app}"; Flags: ignoreversion

; Data files
Source: "bin\Debug\net48\Data\standard_parts.json"; DestDir: "{app}\Data"; Flags: ignoreversion

; Config (generated)
Source: "bin\Debug\net48\plugin_config.json"; DestDir: "{app}"; Flags: ignoreversion onlyifdoesntexist

[Icons]
Name: "{group}\卸载 SW AI Plugin"; Filename: "{uninstallexe}"

[Registry]
; Register COM DLL
Root: HKCR; Subkey: "CLSID\{{B3E7D8A1-4F2C-4A91-B5D6-E8F0A1C2D3E4}"; Flags: uninsdeletekey

[Run]
Filename: "{win}\Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe"; Parameters: """{app}\SwComAddin.dll"" /codebase /tlb"; StatusMsg: "正在注册 SolidWorks 插件..."; Flags: runhidden

[UninstallRun]
Filename: "{win}\Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe"; Parameters: """{app}\SwComAddin.dll"" /unregister /tlb"; StatusMsg: "正在卸载插件..."; Flags: runhidden

[Code]
procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    MsgBox('安装完成！'#13#10#13#10'请按以下步骤启用插件：'#13#10'1. 打开 SolidWorks'#13#10'2. 菜单 工具 → 插件'#13#10'3. 勾选 "SolidWorks AI Plugin"'#13#10'4. 右侧 TaskPane 即可看到插件面板', mbInformation, MB_OK);
  end;
end;
