using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace VideoManager3_WinUI {
    // Tagを表すデータモデル
    public class TagItem:INotifyPropertyChanged {
        public event PropertyChangedEventHandler? PropertyChanged;

        public TagItem() {
            TagVideoItem.CollectionChanged += TagVideoItem_CollectionChanged;
        }

        // データベースの主キー
        private int _id;
        public int Id {
            get => _id;
            set { _id = value; PropertyChanged?.Invoke( this, new PropertyChangedEventArgs( nameof( Id ) ) ); }
        }

        // タグ/グループの名前
        private string _name = "";
        public string Name {
            get => _name;
            set {
                _name = value;
                PropertyChanged?.Invoke( this, new PropertyChangedEventArgs( nameof( Name ) ) );
            }
        }

        // 表示用のタグ/グループの文字色
        private Brush _textColor = new SolidColorBrush( Colors.Black );
        public Brush TextColor {
            get => _textColor;
            set {
                _textColor = value;
                PropertyChanged?.Invoke( this, new PropertyChangedEventArgs( nameof( TextColor ) ) );
            }
        }

        // 表示用のタグ/グループの色
        private Brush? _color;
        public Brush? Color {
            get => _color;
            set {
                _color = value;
                PropertyChanged?.Invoke( this, new PropertyChangedEventArgs( nameof( Color ) ) );
                // Colorプロパティが変更されたときにに文字色も更新
                TextColor = GetContrastingTextBrush( value );
                // Colorプロパティが変更されたときにColorCodeも更新
                ColorCode = value is SolidColorBrush solidColorBrush
                    ? $"#{solidColorBrush.Color.A:X2}{solidColorBrush.Color.R:X2}{solidColorBrush.Color.G:X2}{solidColorBrush.Color.B:X2}"
                    : null;
            }
        }

        // DB保存用のタグ/グループのカラーコード（例: "#FF0000"）
        private string? _colorCode;
        public string? ColorCode {
            get => _colorCode;
            set {
                if ( _colorCode != value ) {
                    _colorCode = value;
                    PropertyChanged?.Invoke( this, new PropertyChangedEventArgs( nameof( ColorCode ) ) );
                    // ColorCodeが変更されたときにColorプロパティも更新
                    Color = ConvertStringToBrush( value );
                }
            }
        }

        // 親タグのID
        private int? _parentId;
        public int? ParentId {
            get => _parentId;
            set {
                _parentId = value;
                PropertyChanged?.Invoke( this, new PropertyChangedEventArgs( nameof( ParentId ) ) );
            }
        }

        // グループ内での順序
        private int _orderInGroup;
        public int OrderInGroup {
            get => _orderInGroup;
            set {
                _orderInGroup = value;
                PropertyChanged?.Invoke( this, new PropertyChangedEventArgs( nameof( OrderInGroup ) ) );
            }
        }

        // グループかどうか
        private bool _isGroup;
        public bool IsGroup {
            get => _isGroup;
            set { _isGroup = value; PropertyChanged?.Invoke( this, new PropertyChangedEventArgs( nameof( IsGroup ) ) ); }
        }

        // タグ編集中かどうか
        private bool _isEditing = false;
        public bool IsEditing {
            get => _isEditing;
            set {
                if ( _isGroup == true ) return; // グループは編集不可
                _isEditing = value;
                PropertyChanged?.Invoke( this, new PropertyChangedEventArgs( nameof( IsEditing ) ) );
                PropertyChanged?.Invoke( this, new PropertyChangedEventArgs( nameof( IsNotEditing ) ) );
            }
        }
        public bool IsNotEditing => !IsEditing;

        // 動画にこのタグが付けられているかどうかを示すフラグ
        private bool _isChecked;
        public bool IsChecked {
            get => _isChecked;
            set {
                if ( _isChecked != value ) {
                    _isChecked = value;
                    PropertyChanged?.Invoke( this, new PropertyChangedEventArgs( nameof( IsChecked ) ) );
                }
            }
        }

        // TreeViewの展開状態
        private bool _isExpanded = true;
        public bool IsExpanded {
            get => _isExpanded;
            set {
                if ( _isExpanded != value ) {
                    _isExpanded = value;
                    IsModified = true;
                    PropertyChanged?.Invoke( this, new PropertyChangedEventArgs( nameof( IsExpanded ) ) );
                }
            }
        }

        // 保存が必要か
        public bool IsModified = false;

        //  タグに関連付けられた動画アイテムのリスト
        public ObservableCollection<VideoItem> TagVideoItem { get; } = new ObservableCollection<VideoItem>();

        // タグに関連付けられた動画の数（子孫の数を合算）
        public int TagVideoCount {
            get {
                int tagVideoCount = TagVideoItem.Count;
                foreach ( var child in Children ) {
                    tagVideoCount += child.TagVideoCount; // 子タグの動画数を合算
                }
                return tagVideoCount;
            }
        }

        /// <summary>
        /// TagVideoItemの中身が変更されたときに呼び出されるイベントハンドラ
        /// </summary>
        private void TagVideoItem_CollectionChanged( object? sender, NotifyCollectionChangedEventArgs e ) {
            // TagVideoItemの中身が変更されたら、TagVideoCountプロパティも変更されたことをUIに通知
            PropertyChanged?.Invoke( this, new PropertyChangedEventArgs( nameof( TagVideoCount ) ) );
        }


        private int calcTagVideoCount() {
            int count = TagVideoItem.Count;
            foreach ( var child in Children ) {
                count += child.calcTagVideoCount(); // 子タグの動画数を合算
            }
            return count;
        }

        // 階層構造のための子要素
        public ObservableCollection<TagItem> Children { get; set; } = new ObservableCollection<TagItem>();

        /// <summary>
        /// 子要素まで探索して、すべてのIDを取得する
        /// </summary>
        /// <returns>このタグとそのすべての子タグのIDのリスト</returns>
        public List<int> GetAllDescendantIds() {
            var ids = new List<int> { Id };
            foreach ( var child in Children ) {
                ids.AddRange( child.GetAllDescendantIds() );
            }
            return ids;
        }

        // 色に合わせた文字色を設定する
        private Brush GetContrastingTextBrush( Brush? background ) {
            if ( background is SolidColorBrush solidColorBrush ) {
                var color = solidColorBrush.Color;
                // 輝度を計算 (0.299*R + 0.587*G + 0.114*B)
                double brightness = (0.299 * color.R + 0.587 * color.G + 0.114 * color.B) / 255;
                // 輝度が0.5以上なら黒、そうでなければ白を返す
                return brightness > 0.5 ? new SolidColorBrush( Colors.Black ) : new SolidColorBrush( Colors.White );
            }
            // デフォルトは黒
            return new SolidColorBrush( Colors.Black );
        }

        // DB用のカラーコードを表示用Brushに変換する
        private static Brush? ConvertStringToBrush( string? colorString ) {
            // 文字列がnull、空、または正しい形式でない場合はnullを返します
            if ( string.IsNullOrEmpty( colorString ) || !colorString.StartsWith( "#" ) || (colorString.Length != 7 && colorString.Length != 9) ) {
                return null;
            }

            try {
                var s = colorString.Substring(1);
                byte a = 255;
                byte r, g, b;

                if ( s.Length == 8 ) // #AARRGGBB形式の場合
                {
                    a = byte.Parse( s.Substring( 0, 2 ), System.Globalization.NumberStyles.HexNumber );
                    s = s.Substring( 2 );
                }

                // #RRGGBB形式のパース
                r = byte.Parse( s.Substring( 0, 2 ), System.Globalization.NumberStyles.HexNumber );
                g = byte.Parse( s.Substring( 2, 2 ), System.Globalization.NumberStyles.HexNumber );
                b = byte.Parse( s.Substring( 4, 2 ), System.Globalization.NumberStyles.HexNumber );

                return new SolidColorBrush( ColorHelper.FromArgb( a, r, g, b ) );
            } catch ( System.Exception ) {
                // パースに失敗した場合（例：不正な16進数文字）はnullを返します
                return null;
            }
        }
    }
}

