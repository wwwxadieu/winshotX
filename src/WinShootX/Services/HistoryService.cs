using System.Collections.ObjectModel;
using WinShootX.Models;

namespace WinShootX.Services;

/// <summary>Giữ danh sách các lần chụp gần đây trong phiên làm việc hiện tại (in-memory, không lưu ổ đĩa).
/// TODO(nâng cao): nếu cần history bền vững qua các lần mở app, lưu thêm đường dẫn file PNG đã save
/// (FileService.SavePng) kèm timestamp vào 1 file index JSON trong %AppData%\WinShootX\history.json.</summary>
public sealed class HistoryService
{
    private const int MaxItems = 20;

    public ObservableCollection<CaptureResult> RecentCaptures { get; } = new();

    public void Add(CaptureResult capture)
    {
        RecentCaptures.Insert(0, capture);
        while (RecentCaptures.Count > MaxItems)
            RecentCaptures.RemoveAt(RecentCaptures.Count - 1);
    }
}
