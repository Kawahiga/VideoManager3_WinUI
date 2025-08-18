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

        // アーティスト名
        public string Name { get; set; } = string.Empty;
        
        // アーティストが関連付けられた動画のリスト
        public List<VideoItem> VideosInArtist { get; set; } = new List<VideoItem>();

        // 表示用の色
        private Brush? _color;
        public Brush? Color {
            get => _color;
            set {
                _color = value;
                PropertyChanged?.Invoke( this, new PropertyChangedEventArgs( nameof( Color ) ) );
            }
        }

    }
}
