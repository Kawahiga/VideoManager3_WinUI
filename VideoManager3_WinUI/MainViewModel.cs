using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using System.Collections.ObjectModel;

namespace VideoManager3_WinUI;

public class MainViewModel
{
    public ObservableCollection<TagItem> TagItems { get; } = new();

    public MainViewModel()
    {
        LoadDummyTags();
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