using System.Diagnostics;
using System.IO;
using System.Timers;
using System.Windows;
using Timer = System.Timers.Timer;

namespace WinShootX.Services;

/// <summary>
/// Quay màn hình xuất MP4 (+ xuất thêm GIF theo yêu cầu), tương đương tính năng quay của CleanShot X.
///
/// Cách triển khai: chạy <c>ffmpeg</c> làm subprocess với input <c>gdigrab</c> (chụp màn hình qua GDI,
/// không cần GPU/driver đặc biệt, chạy được trên mọi máy Windows) thay vì tự viết interop
/// Windows.Graphics.Capture + Media Foundation Sink Writer bằng tay. Lý do chọn hướng này thay vì
/// hướng "thuần WinRT" từng nêu trong outline cũ: encode video low-level qua D3D11/Media Foundation
/// cần rất nhiều COM interop tinh vi (device D3D11, staging texture, IMFSinkWriter...) — rủi ro cao
/// nếu viết mà không kiểm thử được trên máy Windows thật, trong khi gdigrab qua ffmpeg là cách làm
/// phổ biến, ổn định, đã được hàng triệu người dùng qua nhiều năm. Đánh đổi: cần cài ffmpeg riêng
/// (xem <see cref="ResolveFfmpegPath"/>) và không tăng tốc bằng GPU — chấp nhận được cho MVP.
///
/// GIF xuất bằng 2 pha palettegen/paletteuse của ffmpeg (giảm số màu qua bảng màu tối ưu) — đúng tinh
/// thần "palette quantization" nêu trong outline cũ, chỉ khác là dùng bộ lọc có sẵn của ffmpeg thay vì
/// tự cài thuật toán octree/median-cut.
///
/// Chưa triển khai trong bản này: thu âm thanh hệ thống/microphone (<see cref="RecordingOptions"/> có
/// sẵn field nhưng <see cref="StartAsync"/> báo lỗi rõ ràng nếu bật, thay vì âm thầm bỏ qua) — cần xác
/// định chính xác driver loopback audio hoạt động ổn định trên ffmpeg Windows trước khi bật, để tránh
/// một tính năng "có vẻ chạy" nhưng file xuất ra câm hoặc lỗi ở một số máy.
/// </summary>
public sealed class ScreenRecordingService
{
    public bool IsRecording { get; private set; }

    public event Action<TimeSpan>? RecordingTimeChanged;

    private Process? _ffmpegProcess;
    private string? _outputPath;
    private DateTime _startedAtUtc;
    private Timer? _progressTimer;

    public Task StartAsync(Int32Rect region, RecordingOptions options, string outputDirectory)
    {
        if (IsRecording)
            throw new InvalidOperationException("Đang quay màn hình rồi — dừng bản ghi hiện tại trước.");

        if (options.CaptureSystemAudio || options.CaptureMicrophone)
            throw new NotSupportedException("Thu âm thanh chưa được hỗ trợ trong bản này — chỉ quay hình.");

        var ffmpegPath = ResolveFfmpegPath()
            ?? throw new FileNotFoundException(
                "Không tìm thấy ffmpeg.exe. Cài ffmpeg (https://ffmpeg.org/download.html) và thêm vào PATH hệ thống, " +
                "hoặc đặt file ffmpeg.exe cùng thư mục với WinShootX.exe.");

        Directory.CreateDirectory(outputDirectory);
        _outputPath = Path.Combine(outputDirectory, $"WinShootX_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.mp4");

        // -framerate 30: đủ mượt cho ghi màn hình demo/hướng dẫn sử dụng, không quá nặng CPU khi encode
        // bằng preset ultrafast. yuv420p đảm bảo file phát được trên hầu hết trình phát/trình duyệt.
        var arguments =
            $"-y -f gdigrab -framerate 30 -offset_x {region.X} -offset_y {region.Y} " +
            $"-video_size {region.Width}x{region.Height} -i desktop " +
            $"-c:v libx264 -preset ultrafast -pix_fmt yuv420p \"{_outputPath}\"";

        _ffmpegProcess = new Process
        {
            StartInfo = new ProcessStartInfo(ffmpegPath, arguments)
            {
                RedirectStandardInput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
            EnableRaisingEvents = true,
        };
        _ffmpegProcess.Start();

        IsRecording = true;
        _startedAtUtc = DateTime.UtcNow;

        _progressTimer = new Timer(500);
        _progressTimer.Elapsed += OnProgressTick;
        _progressTimer.Start();

        return Task.CompletedTask;
    }

    private void OnProgressTick(object? sender, ElapsedEventArgs e) =>
        RecordingTimeChanged?.Invoke(DateTime.UtcNow - _startedAtUtc);

    /// <summary>Dừng quay và trả về đường dẫn file MP4. Gửi phím 'q' vào stdin của ffmpeg thay vì
    /// Kill() để nó tự đóng file đúng cách (finalize moov atom) — Kill() giữa chừng dễ tạo ra file
    /// MP4 hỏng/không phát được.</summary>
    public async Task<string> StopAsync()
    {
        if (!IsRecording || _ffmpegProcess == null || _outputPath == null)
            throw new InvalidOperationException("Chưa bắt đầu quay màn hình.");

        _progressTimer?.Stop();
        _progressTimer?.Dispose();
        _progressTimer = null;

        try
        {
            await _ffmpegProcess.StandardInput.WriteAsync("q");
            await _ffmpegProcess.StandardInput.FlushAsync();
        }
        catch (IOException)
        {
            // Process có thể đã tự thoát (vd người dùng đóng bằng tay) — không sao, vẫn chờ exit bên dưới.
        }

        await _ffmpegProcess.WaitForExitAsync();

        var outputPath = _outputPath;
        _ffmpegProcess.Dispose();
        _ffmpegProcess = null;
        _outputPath = null;
        IsRecording = false;

        return outputPath;
    }

    public async Task<string> ExportGifAsync(string mp4Path, int fps = 12, int maxWidth = 720)
    {
        var ffmpegPath = ResolveFfmpegPath()
            ?? throw new FileNotFoundException("Không tìm thấy ffmpeg.exe.");

        var gifPath = Path.ChangeExtension(mp4Path, ".gif");
        var palettePath = Path.Combine(Path.GetTempPath(), $"winshootx_palette_{Guid.NewGuid():N}.png");

        try
        {
            await RunFfmpegAsync(ffmpegPath,
                $"-y -i \"{mp4Path}\" -vf \"fps={fps},scale={maxWidth}:-1:flags=lanczos,palettegen\" \"{palettePath}\"");
            await RunFfmpegAsync(ffmpegPath,
                $"-y -i \"{mp4Path}\" -i \"{palettePath}\" " +
                $"-filter_complex \"fps={fps},scale={maxWidth}:-1:flags=lanczos[x];[x][1:v]paletteuse\" \"{gifPath}\"");
        }
        finally
        {
            if (File.Exists(palettePath)) File.Delete(palettePath);
        }

        return gifPath;
    }

    private static async Task RunFfmpegAsync(string ffmpegPath, string arguments)
    {
        using var process = Process.Start(new ProcessStartInfo(ffmpegPath, arguments)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        }) ?? throw new InvalidOperationException("Không khởi chạy được ffmpeg.");

        await process.WaitForExitAsync();
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"ffmpeg thoát với mã lỗi {process.ExitCode}.");
    }

    private static string? ResolveFfmpegPath()
    {
        var bundled = Path.Combine(AppContext.BaseDirectory, "ffmpeg.exe");
        if (File.Exists(bundled)) return bundled;

        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var dir in pathEnv.Split(Path.PathSeparator))
        {
            if (dir.Length == 0) continue;
            var candidate = Path.Combine(dir, "ffmpeg.exe");
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }
}

public sealed class RecordingOptions
{
    public bool CaptureSystemAudio { get; set; }
    public bool CaptureMicrophone { get; set; }
}
