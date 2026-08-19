using System.IO;
using System.Text.Json;
using WinShootX.Models;

namespace WinShootX.Services;

public sealed class SettingsService
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "WinShootX", "settings.json");

    public CaptureSettings Current { get; private set; } = new();

    public void Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                Current = JsonSerializer.Deserialize<CaptureSettings>(json) ?? new CaptureSettings();
            }
        }
        catch (IOException)
        {
            // File hỏng/đang bị khoá: dùng mặc định, không crash app.
            Current = new CaptureSettings();
        }
        catch (JsonException)
        {
            Current = new CaptureSettings();
        }
    }

    public void Save()
    {
        var dir = Path.GetDirectoryName(SettingsPath)!;
        Directory.CreateDirectory(dir);
        var json = JsonSerializer.Serialize(Current, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(SettingsPath, json);
    }
}
