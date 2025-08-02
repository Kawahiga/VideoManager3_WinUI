using Microsoft.UI.Xaml.Media;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace VideoManager3_WinUI
{
    // Tagを表すデータモデル
    public class TagItem : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        // データベースの主キー
        private int _id;
        public int Id
        {
            get => _id;
            set { _id = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Id))); }
        }

        // タグ/グループの名前
        private string _name = "";
        public string Name
        {
            get => _name;
            set { _name = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name))); }
        }

        // 表示用のタグ/グループの色
        private Brush? _color;
        public Brush? Color
        {
            get => _color;
            set { _color = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Color))); }
        }

        // DB保存用のタグ/グループのカラーコード（例: "#FF0000"）
        private string? _colorCode;
        public string? ColorCode
        {
            get => _colorCode;
            set { _colorCode = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ColorCode))); }
        }

        // 親タグのID
        private int? _parentId;
        public int? ParentId
        {
            get => _parentId;
            set { _parentId = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ParentId))); }
        }

        // グループ内での順序
        private int _orderInGroup;
        public int OrderInGroup
        {
            get => _orderInGroup;
            set { _orderInGroup = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(OrderInGroup))); }
        }

        // グループかどうか
        private bool _isGroup;
        public bool IsGroup
        {
            get => _isGroup;
            set { _isGroup = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsGroup))); }
        }

        // 階層構造のための子要素
        public ObservableCollection<TagItem> Children { get; set; } = new ObservableCollection<TagItem>();
    }
}

