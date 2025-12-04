using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VideoManager3_WinUI.Models {
    public class ArtistItem:INotifyPropertyChanged {
        public event PropertyChangedEventHandler? PropertyChanged;

        public ArtistItem() {
            VideosInArtist.CollectionChanged += VideosInArtist_CollectionChanged;
        }

        // アーティストID
        public int Id { get; set; } = 0;

        // 表示用アーティスト名
        // 例: "浜崎りお(篠原絵梨香、森下えりか)"
        private string _name = string.Empty;
        public string Name {
            get => _name;
            set {
                if ( _name != value ) {
                    _name = value;
                    PropertyChanged?.Invoke( this, new PropertyChangedEventArgs( nameof( Name ) ) );
                }
            }
        }

        // アーティスの名義リスト(主名が先頭)
        // 例: ["浜崎りお", "篠原絵梨香", "森下えりか"]
        public List<string> AliaseNames { get; set; } = new List<string>();

        // 主名
        public string PrimaryName => AliaseNames.FirstOrDefault() ?? string.Empty;


        // お気に入り
        private bool _isFavorite = false;
        public bool IsFavorite {
            get => _isFavorite;
            set {
                _isFavorite = value;
                if ( _isFavorite == false )
                    ArtistColor = new SolidColorBrush( Colors.DarkSlateGray );
                else {
                    ArtistColor = new SolidColorBrush( Colors.Pink );
                }
                PropertyChanged?.Invoke( this, new PropertyChangedEventArgs( nameof( IsFavorite ) ) );
            }
        }

        // いいね数
        private int _likeCount = 0;
        public int LikeCount {
            get => _likeCount;
            set {
                if ( _likeCount != value ) {
                    _likeCount = value;
                    PropertyChanged?.Invoke( this, new PropertyChangedEventArgs( nameof( LikeCount ) ) );
                }
            }
        }

        // アイコンパス (例: "icon.png")
        public string IconPath { get; set; } = string.Empty;

        // 表示用の文字色
        private Brush _textColor = new SolidColorBrush( Colors.Black );
        public Brush TextColor {
            get => _textColor;
            set {
                _textColor = value;
                PropertyChanged?.Invoke( this, new PropertyChangedEventArgs( nameof( TextColor ) ) );
            }
        }

        // 表示用の色
        private Brush _artistColor = new SolidColorBrush( Colors.DarkGray );
        public Brush ArtistColor {
            get => _artistColor;
            set {
                _artistColor = value;
                // Colorプロパティが変更されたときにに文字色も更新
                TextColor = GetContrastingTextBrush( value );
                PropertyChanged?.Invoke( this, new PropertyChangedEventArgs( nameof( ArtistColor ) ) );
            }
        }

        // アーティストが関連付けられた動画のリスト
        public ObservableCollection<VideoItem> VideosInArtist { get; } = new ObservableCollection<VideoItem>();

        // VideoCountはVideosInArtistの要素数を返す読み取り専用プロパティ
        public int VideoCount => VideosInArtist.Count;

        // フィルター適用後の動画数
        public int FilteredVideoCount => TempFilteredCount;

        private int _tempFilteredCount = 0;
        public int TempFilteredCount {
            get => _tempFilteredCount;
            set {
                if ( _tempFilteredCount != value ) {
                    _tempFilteredCount = value;
                    PropertyChanged?.Invoke( this, new PropertyChangedEventArgs( nameof( FilteredVideoCount ) ) );
                }
            }
        }

        private void VideosInArtist_CollectionChanged( object? sender, NotifyCollectionChangedEventArgs e ) {
            // VideosInArtistの中身が変更されたら、VideoCountプロパティも変更されたことをUIに通知
            PropertyChanged?.Invoke( this, new PropertyChangedEventArgs( nameof( VideoCount ) ) );
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
    }
}