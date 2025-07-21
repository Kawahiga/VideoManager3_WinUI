using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using System.Collections.ObjectModel;

namespace VideoManager3_WinUI;

public partial class MainViewModel : ObservableObject
{
    // 左ペインのタグツリー用アイテム
    public ObservableCollection<TagItem> TagItems { get; } = new();

    // 右ペインの動画一覧用アイテム
    public ObservableCollection<VideoItem> VideoItems { get; } = new();

    public MainViewModel()
    {
        LoadDummyTags();
        LoadDummyVideos(); // ダミーの動画データを読み込むメソッドを呼び出します。
    }

    // 右ペインに表示する大量のダミーデータを生成します。
    // UI仮想化の効果を確認するため、10,000件のアイテムを作成します。
    private void LoadDummyVideos()
    {
        for (int i = 1; i <= 10000; i++)
        {
            // VideoItemのプロパティ名(FileName, FilePath)はソースジェネレーターによって生成されるため、変更の必要はありません。
            VideoItems.Add(new VideoItem { FileName = $"Video_{i:D5}.mp4", FilePath = $"C:\\Dummy\\Path\\Video_{i:D5}.mp4" });
        }
    }

    private void LoadDummyTags()
    {
        var group1 = new TagItem
        {
            Name = "ジャンル",
            Color = new SolidColorBrush(Colors.CornflowerBlue),
            Children =
            {
                new TagItem { Name = "アクション", Color = new SolidColorBrush(Colors.OrangeRed) },
                new TagItem { Name = "コメディ", Color = new SolidColorBrush(Colors.Gold) },
            }
        };

        var group2 = new TagItem
        {
            Name = "製作者",
            Color = new SolidColorBrush(Colors.SeaGreen),
            Children =
            {
                new TagItem
                {
                    Name = "スタジオA",
                    Color = new SolidColorBrush(Colors.LightGreen),
                    Children =
                    {
                        new TagItem { Name = "監督X", Color = new SolidColorBrush(Colors.Turquoise) },
                        new TagItem { Name = "監督Y", Color = new SolidColorBrush(Colors.Turquoise) }
                    }
                },
                new TagItem { Name = "スタジオB", Color = new SolidColorBrush(Colors.LightGreen) },
            }
        };

        var singleTag = new TagItem { Name = "お気に入り", Color = new SolidColorBrush(Colors.HotPink) };

        TagItems.Add(group1);
        TagItems.Add(group2);
        TagItems.Add(singleTag);
    }
}
