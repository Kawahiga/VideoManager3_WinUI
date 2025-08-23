using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VideoManager3_WinUI {
    public class ArtistItem:INotifyPropertyChanged {
        public event PropertyChangedEventHandler? PropertyChanged;

        // アーティストID
        public int Id { get; set; } = 0;

        // アーティスト名
        public string Name { get; set; } = string.Empty;

        // お気に入り
        private bool _isFavorite = false;
        public bool IsFavorite {
            get => _isFavorite;
            set {
                _isFavorite = value;
                if ( _isFavorite == false ) {
                    ArtistColor = new SolidColorBrush( Colors.DarkSlateGray );
                } else {
                    ArtistColor = new SolidColorBrush( Colors.Pink );
                }
            }
        }

        // アイコンパス (例: "icon.png")
        public string IconPath { get; set; } = string.Empty;

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

        // 表示用の文字色
        private Brush _textColor = new SolidColorBrush( Colors.Black );
        public Brush TextColor {
            get => _textColor;
            set {
                _textColor = value;
                PropertyChanged?.Invoke( this, new PropertyChangedEventArgs( nameof( TextColor ) ) );
            }
        }

        // アーティストが関連付けられた動画のリスト
        public List<VideoItem> VideosInArtist { get; set; } = new List<VideoItem>();

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
