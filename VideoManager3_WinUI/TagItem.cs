using Microsoft.UI.Xaml.Media;
using System.Collections.ObjectModel;

namespace VideoManager3_WinUI;

public class TagItem
{
    // タグ/グループの名前
    public string Name { get; set; }

    // 子アイテムのコレクション（階層構造のため）
    public ObservableCollection<TagItem> Children { get; set; } = new();

    // タグ/グループの色
    public Brush Color { get; set; }
}
