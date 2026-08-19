; Inno Setup script cho Win ShootX — thay cho Velopack (xem PRD/README lý do đổi): Velopack (cả bản
; Setup.exe lẫn .msi) không cho người dùng chọn thư mục cài tuỳ ý qua giao diện, chỉ có 2 vị trí cố
; định (LocalAppData hoặc Program Files) — giới hạn kiến trúc để auto-update hoạt động, không sửa
; được bằng cấu hình. Inno Setup cho "Select Destination Location" (nút Browse...) sẵn có, đúng nhu
; cầu người dùng.
;
; PrivilegesRequired=lowest + cài mặc định vào {localappdata}\Programs\WinShootX: không cần quyền
; admin — quan trọng để auto-update (Services/AppUpdateService.cs) chạy installer ở chế độ im lặng mà
; không bị Windows chặn lại hỏi UAC giữa chừng. Người dùng vẫn có thể Browse sang bất kỳ thư mục nào
; tài khoản hiện tại có quyền ghi (kể cả Program Files nếu chạy installer với quyền admin thủ công).
;
; Build bằng: ISCC.exe installer\WinShootX.iss /DMyAppVersion=X.Y.Z (xem .github/workflows/release.yml)

#ifndef MyAppVersion
  #define MyAppVersion "0.0.1"
#endif

#define MyAppName "Win ShootX"
#define MyAppPublisher "wwwxadieu"
#define MyAppExeName "WinShootX.exe"

[Setup]
; NOTE: AppId phải giữ nguyên GUID này qua mọi phiên bản để Inno Setup nhận diện đúng là bản nâng
; cấp (upgrade) thay vì coi là 1 app khác khi người dùng cài đè bản mới lên bản cũ.
; Viết thẳng GUID tại đây (không qua #define + {#macro}) để tránh 2 lớp escape chồng nhau giữa ISPP
; và core compiler — nguồn gốc 2 lần build lỗi trước đó. Ở cấp core compiler, "{{" là escape cho 1
; dấu "{" thật; theo sau là GUID thô, đóng lại bởi dấu "}" cuối vốn có của chính GUID. Kết quả sau
; khi dựng là "{081F9E36-3D78-4E7E-8D7B-E44B7D942AC1}" — đúng định dạng AppId cần. Đây là cách viết
; tiêu chuẩn mà chính Inno Setup Wizard sinh ra khi tạo project mới.
AppId={{081F9E36-3D78-4E7E-8D7B-E44B7D942AC1}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppUpdatesURL=https://github.com/wwwxadieu/winshotX/releases
DefaultDirName={localappdata}\Programs\WinShootX
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\installer-output
; Tên file CỐ ĐỊNH qua mọi phiên bản (không kèm số bản) — AppUpdateService.cs dựa vào tên này để tìm
; đúng asset installer trong GitHub Release mới nhất, không cần biết trước số phiên bản kế tiếp.
OutputBaseFilename=WinShootX-Setup
SetupIconFile=..\src\WinShootX\Assets\app.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Tạo shortcut ngoài Desktop"; GroupDescription: "Shortcut bổ sung:"

[Files]
Source: "..\publish\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
; Không có flag "skipifsilent": installer chạy /VERYSILENT (auto-update, xem AppUpdateService.cs)
; cũng tự mở lại app sau khi cài xong, không chỉ khi cài thủ công qua wizard.
Filename: "{app}\{#MyAppExeName}"; Description: "Khởi chạy {#MyAppName}"; Flags: nowait postinstall
