using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media;

namespace VideoManager3_WinUI;

public partial class VideoItem : ObservableObject
{
    // プライベートなフィールドを定義
    private string? _fileName;
    private string? _filePath;
    private ImageSource? _thumbnail;

    // ファイル名のプロパティ
    // [ObservableProperty]を使わず、手動でプロパティを定義します。
    // setの中でSetPropertyを呼び出すことで、値が変更されたことをUIに通知します。
    public string? FileName
    {
        get => _fileName;
        set => SetProperty(ref _fileName, value);
    }

    // ファイルパスのプロパティ
    public string? FilePath
    {
        get => _filePath;
        set => SetProperty(ref _filePath, value);
    }

    // サムネイル画像のプロパティ
    public ImageSource? Thumbnail
    {
        get => _thumbnail;
        set => SetProperty(ref _thumbnail, value);
    }
}
