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
                    _artistColor = new SolidColorBrush( Colors.DarkGray );
                } else {
                    _artistColor = new SolidColorBrush( Colors.DeepPink );
                }
            }
        }

        // アイコンパス (例: "icon.png")
        public string IconPath { get; set; } = string.Empty;

        // 表示用の色
        private Brush? _artistColor;
        public Brush? ArtistColor {
            get => _artistColor;
            set {
                _artistColor = value;
                PropertyChanged?.Invoke( this, new PropertyChangedEventArgs( nameof( ArtistColor ) ) );
            }
        }

        // アーティストが関連付けられた動画のリスト
        public List<VideoItem> VideosInArtist { get; set; } = new List<VideoItem>();
    }
}
