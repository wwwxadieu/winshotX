using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace WinShootX.Services;

/// <summary>
/// Đăng ký tổ hợp phím tắt toàn cục (hoạt động kể cả khi app không có cửa sổ focus) qua
/// Win32 RegisterHotKey. Cần một HWND ẩn để nhận WM_HOTKEY — App.xaml.cs tạo một
/// MessageOnlyWindow riêng cho việc này.
/// </summary>
public sealed class HotkeyManager : IDisposable
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private const int WM_HOTKEY = 0x0312;

    [Flags]
    private enum Modifiers : uint
    {
        Alt = 0x1,
        Control = 0x2,
        Shift = 0x4,
        Win = 0x8,
        NoRepeat = 0x4000,
    }

    private readonly Dictionary<int, Action> _handlers = new();
    private readonly HwndSource _hwndSource;
    private int _nextId = 0xB001; // vùng id tuỳ ý tránh đụng id hệ thống

    public HotkeyManager()
    {
        // Cửa sổ message-only: không hiện UI, chỉ dùng để bơm message loop nhận WM_HOTKEY.
        var parameters = new HwndSourceParameters("WinShootXHotkeySink")
        {
            WindowStyle = 0,
            ExtendedWindowStyle = 0x80, // WS_EX_TOOLWINDOW, không hiện trên taskbar
            Width = 0,
            Height = 0,
        };
        _hwndSource = new HwndSource(parameters);
        _hwndSource.AddHook(WndProc);
    }

    /// <summary>Đăng ký hotkey dạng chuỗi "Ctrl+Shift+4" và callback khi được bấm.</summary>
    public bool Register(string hotkeyText, Action callback)
    {
        if (!TryParse(hotkeyText, out var modifiers, out var vk))
            return false;

        int id = _nextId++;
        bool ok = RegisterHotKey(_hwndSource.Handle, id, (uint)modifiers | (uint)Modifiers.NoRepeat, vk);
        if (ok)
            _handlers[id] = callback;
        return ok;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && _handlers.TryGetValue((int)wParam, out var cb))
        {
            handled = true;
            cb();
        }
        return IntPtr.Zero;
    }

    private static bool TryParse(string text, out Modifiers modifiers, out uint vk)
    {
        modifiers = 0;
        vk = 0;
        var parts = text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return false;

        foreach (var part in parts[..^1])
        {
            modifiers |= part.ToLowerInvariant() switch
            {
                "ctrl" or "control" => Modifiers.Control,
                "alt" => Modifiers.Alt,
                "shift" => Modifiers.Shift,
                "win" or "windows" => Modifiers.Win,
                _ => 0,
            };
        }

        var keyText = parts[^1].ToUpperInvariant();
        // Số/chữ ánh xạ trực tiếp sang VK (đủ dùng cho hotkey mặc định 0-9, A-Z, F1-F12).
        if (keyText.Length == 1 && (char.IsDigit(keyText[0]) || char.IsLetter(keyText[0])))
        {
            vk = keyText[0];
            return true;
        }
        if (keyText.StartsWith('F') && int.TryParse(keyText.AsSpan(1), out var fn) && fn is >= 1 and <= 24)
        {
            vk = (uint)(0x6F + fn); // VK_F1 = 0x70
            return true;
        }
        return false;
    }

    public void Dispose()
    {
        foreach (var id in _handlers.Keys)
            UnregisterHotKey(_hwndSource.Handle, id);
        _handlers.Clear();
        _hwndSource.RemoveHook(WndProc);
        _hwndSource.Dispose();
    }
}
