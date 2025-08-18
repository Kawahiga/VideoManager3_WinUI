using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Storage.Streams;

namespace VideoManager3_WinUI {
    public class VideoItem:INotifyPropertyChanged {
        public int Id { get; set; } // データベースの主キー
        public string? FileName { get; set; }
        public string? FilePath { get; set; }
        public string? Extension { get; set; }
        public long FileSize { get; set; }  // ファイルサイズ（バイト単位）
        public DateTime LastModified { get; set; }  // 最終更新日時（YYYY/MM/dd HH:mm:ss形式）
        public double Duration { get; set; }    // 動画の再生時間（秒）

        private int _likeCount = 0; // いいね数
        public int LikeCount {
            get => _likeCount;
            set {
                if ( _likeCount != value ) {
                    _likeCount = value;
                    OnPropertyChanged( nameof( LikeCount ) );
                }
            }
        }

        private int _viewCount = 0; // 再生数
        public int ViewCount {
            get => _viewCount;
            set {
                if ( _viewCount != value ) {
                    _viewCount = value;
                    OnPropertyChanged( nameof( ViewCount ) );
                }
            }
        }

        // サムネイルの元データ（軽量なbyte配列）
        public byte[]? Thumbnail { get; set; }

        // UI表示用のサムネイル画像（BitmapImage）
        private BitmapImage? _thumbnailImage;
        public BitmapImage? ThumbnailImage {
            get => _thumbnailImage;
            private set // Viewからの直接の変更は禁止
            {
                if ( _thumbnailImage != value ) {
                    _thumbnailImage = value;
                    OnPropertyChanged( nameof( ThumbnailImage ) );
                }
            }
        }

        // ファイルに設定されたタグ情報
        private ObservableCollection<TagItem> videoTagItems = new ObservableCollection<TagItem>();
        public ObservableCollection<TagItem> VideoTagItems {
            get => videoTagItems;
            set {
                if ( videoTagItems != value ) {
                    videoTagItems = value;
                    OnPropertyChanged( nameof( VideoTagItems ) );
                }
            }
        }

        // ファイルのアーティスト情報
        public ObservableCollection<ArtistItem> ArtistsInVideo = new ObservableCollection<ArtistItem>();

        public VideoItem() { }

        public VideoItem( int id, string filePath, string fileName, long fileSize, DateTime lastModified, double duration ) {
            Id = id;
            FilePath = filePath;
            FileName = fileName;
            FileSize = fileSize;
            LastModified = lastModified;
            Duration = duration;
        }

        // UIスレッドから呼び出されることを前提とした、非同期でのサムネイル画像読み込みメソッド
        public async Task LoadThumbnailImageAsync() {
            // 既に画像がある、または元データがない場合は何もしない
            if ( ThumbnailImage != null || Thumbnail == null || Thumbnail.Length == 0 ) {
                return;
            }

            try {
                var bitmapImage = new BitmapImage();
                using var stream = new InMemoryRandomAccessStream();
                await stream.WriteAsync( Thumbnail.AsBuffer() );
                stream.Seek( 0 );

                // このメソッドはUIスレッドで実行されるため、直接ソースを設定できる
                await bitmapImage.SetSourceAsync( stream );
                ThumbnailImage = bitmapImage;
            } catch ( Exception ex ) {
                System.Diagnostics.Debug.WriteLine( $"Failed to create BitmapImage for {FileName}: {ex.Message}" );
            }
        }

        // 表示されなくなったアイテムのメモリを解放する
        public void UnloadThumbnailImage() {
            ThumbnailImage = null;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged( string propertyName ) {
            PropertyChanged?.Invoke( this, new PropertyChangedEventArgs( propertyName ) );
        }
    }
}
