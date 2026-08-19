using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;

namespace WinShootX.Services;

/// <summary>
/// Auto-update tự viết, thay cho Velopack — vì chuyển sang installer Inno Setup (cho phép chọn thư
/// mục cài qua giao diện, điều Velopack không hỗ trợ dù bản Setup.exe hay .msi, xem README) nên không
/// còn dùng được cơ chế cập nhật có sẵn của Velopack (nó gắn chặt với định dạng nupkg/RELEASES do
/// chính vpk tạo ra). Cách làm ở đây đơn giản hơn nhiều so với Velopack (tải nguyên bản installer
/// mới mỗi lần thay vì patch nhị phân delta), đổi lại dễ hiểu/dễ bảo trì và không phụ thuộc thư viện
/// ngoài nào ngoài BCL.
///
/// Quy trình:
/// 1. Gọi GitHub REST API lấy release mới nhất của repo, so sánh tag với phiên bản đang chạy (đọc từ
///    AssemblyVersion — được .NET SDK tự sinh từ MSBuild property Version khi publish, xem csproj).
/// 2. Nếu có bản mới: tải file installer đúng tên cố định (<see cref="InstallerAssetName"/> — tên này
///    KHÔNG đổi giữa các phiên bản, xem OutputBaseFilename trong installer/WinShootX.iss) về thư mục
///    temp.
/// 3. Chạy installer ở chế độ im lặng (/VERYSILENT) qua 1 lớp trung gian đợi vài giây để app hiện tại
///    kịp thoát hẳn (giải phóng khoá file WinShootX.exe) trước khi installer ghi đè — xem
///    <see cref="RunSilentInstallAndRestart"/>. Installer tự mở lại app sau khi cài xong (xem [Run]
///    trong file .iss, không có flag skipifsilent để áp dụng cho cả luồng im lặng này).
/// </summary>
public sealed class AppUpdateService
{
    private const string LatestReleaseApiUrl = "https://api.github.com/repos/wwwxadieu/winshotX/releases/latest";

    // Tên file installer CỐ ĐỊNH qua mọi phiên bản (không kèm số bản trong tên) — để code này không
    // cần biết trước tên file của bản kế tiếp. Phải khớp OutputBaseFilename trong installer/WinShootX.iss.
    private const string InstallerAssetName = "WinShootX-Setup.exe";

    public sealed record UpdateInfo(string Version, string DownloadUrl);

    public async Task<UpdateInfo?> CheckForUpdateAsync(CancellationToken cancellationToken = default)
    {
        using var http = CreateClient();
        using var response = await http.GetAsync(LatestReleaseApiUrl, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = doc.RootElement;

        var tagName = root.TryGetProperty("tag_name", out var tagProp) ? tagProp.GetString() : null;
        if (string.IsNullOrEmpty(tagName)) return null;

        var latestVersion = tagName.TrimStart('v', 'V');
        if (!IsNewer(latestVersion, GetCurrentVersion())) return null;

        if (!root.TryGetProperty("assets", out var assets)) return null;
        foreach (var asset in assets.EnumerateArray())
        {
            var name = asset.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : null;
            if (!string.Equals(name, InstallerAssetName, StringComparison.OrdinalIgnoreCase)) continue;

            var url = asset.GetProperty("browser_download_url").GetString();
            if (url != null) return new UpdateInfo(latestVersion, url);
        }

        return null; // Release mới không có đúng file installer mong đợi — bỏ qua thay vì lỗi.
    }

    public async Task<string> DownloadInstallerAsync(string downloadUrl, CancellationToken cancellationToken = default)
    {
        using var http = CreateClient();
        var tempPath = Path.Combine(Path.GetTempPath(), $"WinShootX-Setup-{Guid.NewGuid():N}.exe");

        using var response = await http.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await contentStream.CopyToAsync(fileStream, cancellationToken);

        return tempPath;
    }

    /// <summary>Chạy installer im lặng qua cmd (đợi 2s để app hiện tại thoát hẳn trước, tránh lỗi file
    /// đang bị khoá) — gọi ngay trước khi tự Shutdown() app hiện tại.</summary>
    public static void RunSilentInstallAndRestart(string installerPath)
    {
        var arguments = $"/c timeout /t 2 /nobreak >nul & \"{installerPath}\" /VERYSILENT /SUPPRESSMSGBOXES /NORESTART";
        Process.Start(new ProcessStartInfo("cmd.exe", arguments)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        });
    }

    public static string GetCurrentVersion()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        return version == null ? "0.0.0" : $"{version.Major}.{version.Minor}.{version.Build}";
    }

    private static bool IsNewer(string latest, string current) =>
        Version.TryParse(latest, out var lv) && Version.TryParse(current, out var cv) && lv > cv;

    private static HttpClient CreateClient()
    {
        var http = new HttpClient();
        http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("WinShootX", GetCurrentVersion()));
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return http;
    }
}
