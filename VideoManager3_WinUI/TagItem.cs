using Microsoft.UI;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;


// エンハンス案
// 1.タグの展開状態を保存するためのプロパティを追加

namespace VideoManager3_WinUI {
    // Tagを表すデータモデル
    public class TagItem:INotifyPropertyChanged {
        public event PropertyChangedEventHandler? PropertyChanged;

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
            set { _name = value; PropertyChanged?.Invoke( this, new PropertyChangedEventArgs( nameof( Name ) ) ); }
        }

        // 表示用のタグ/グループの色
        private Brush? _color;
        public Brush? Color {
            get => _color;
            set
            {
                _color = value;
                PropertyChanged?.Invoke( this, new PropertyChangedEventArgs( nameof( Color ) ) );
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
            set
            {
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
            set { _parentId = value; PropertyChanged?.Invoke( this, new PropertyChangedEventArgs( nameof( ParentId ) ) ); }
        }

        // グループ内での順序
        private int _orderInGroup;
        public int OrderInGroup {
            get => _orderInGroup;
            set { _orderInGroup = value; PropertyChanged?.Invoke( this, new PropertyChangedEventArgs( nameof( OrderInGroup ) ) ); }
        }

        // グループかどうか
        private bool _isGroup;
        public bool IsGroup {
            get => _isGroup;
            set { _isGroup = value; PropertyChanged?.Invoke( this, new PropertyChangedEventArgs( nameof( IsGroup ) ) ); }
        }

        // 動画にこのタグが付けられているかどうかを示すフラグ
        private bool _isChecked;
        public bool IsChecked {
            get => _isChecked;
            set
            {
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
            set
            {
                if ( _isExpanded != value ) {
                    _isExpanded = value;
                    PropertyChanged?.Invoke( this, new PropertyChangedEventArgs( nameof( IsExpanded ) ) );
                }
            }
        }

        /// <summary>
        ///  タグに関連付けられた動画アイテムのリスト
        /// </summary>
        public ObservableCollection<VideoItem> TagVideoItem { get; set; }

        // 階層構造のための子要素
        public ObservableCollection<TagItem> Children { get; set; } = new ObservableCollection<TagItem>();

        /// <summary>
        /// 子要素まで探索して、指定したIDのタグを取得する
        /// </summary>
        /// <param name="id">検索するタグのID</param>
        /// <returns>見つかったタグ。存在しない場合はnull</returns>
        public TagItem? FindTagById( int id ) {
            if ( Id == id )
                return this;
            foreach ( var child in Children ) {
                var found = child.FindTagById(id);
                if ( found != null )
                    return found;
            }
            return null;
        }

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
            }
            catch ( System.Exception ) {
                // パースに失敗した場合（例：不正な16進数文字）はnullを返します
                return null;
            }
        }
    }
}

