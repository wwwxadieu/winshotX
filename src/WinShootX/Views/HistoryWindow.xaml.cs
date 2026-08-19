using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using WinShootX.Models;
using WinShootX.Services;

namespace WinShootX.Views;

/// <summary>Danh sách các ảnh đã chụp gần đây trong phiên hiện tại — double-click để mở lại trình chú thích.</summary>
public partial class HistoryWindow : Window, INotifyPropertyChanged
{
    private readonly HistoryService _history;

    public Visibility EmptyHintVisibility =>
        _history.RecentCaptures.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    public HistoryWindow(HistoryService history)
    {
        InitializeComponent();
        DataContext = this;
        _history = history;
        ItemsList.ItemsSource = _history.RecentCaptures;
        _history.RecentCaptures.CollectionChanged += (_, _) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(EmptyHintVisibility)));
    }

    private void OnItemDoubleClick(object sender, RoutedEventArgs e)
    {
        if (ItemsList.SelectedItem is CaptureResult capture)
            new AnnotationEditorWindow(capture.Image).Show();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
