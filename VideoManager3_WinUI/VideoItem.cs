using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media;

namespace VideoManager3_WinUI;

public partial class VideoItem : ObservableObject
{
    // ファイル名
    [ObservableProperty]
    private string? _fileName;

    // ファイルパス
    [ObservableProperty]
    private string? _filePath;

    // サムネイル画像
    // 現時点ではダミーですが、将来的には非同期で読み込みます。
    [ObservableProperty]
    private ImageSource? _thumbnail;
}
