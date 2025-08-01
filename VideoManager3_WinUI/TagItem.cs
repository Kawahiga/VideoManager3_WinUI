using Microsoft.UI.Xaml.Media;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace VideoManager3_WinUI
{
    // Tagを表すデータモデル
    public class TagItem : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private int _id;
        public int Id
        {
            get => _id;
            set { _id = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Id))); }
        }

        private string _name = "";
        public string Name
        {
            get => _name;
            set { _name = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name))); }
        }

        // タグ/グループの色
        private Brush? _color;
        public Brush? Color
        {
            get => _color;
            set { _color = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Color))); }
        }

        private int? _parentId;
        public int? ParentId
        {
            get => _parentId;
            set { _parentId = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ParentId))); }
        }

        // 階層構造のための子要素
        public ObservableCollection<TagItem> Children { get; set; } = new ObservableCollection<TagItem>();
    }
}

