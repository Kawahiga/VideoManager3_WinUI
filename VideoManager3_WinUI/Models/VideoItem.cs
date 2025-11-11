using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Storage.Streams;

namespace VideoManager3_WinUI.Models {
    public class VideoItem:INotifyPropertyChanged {

        public VideoItem() {
            // VideoTagItemsの変更を監視
            VideoTagItems.CollectionChanged += VideoTagItems_CollectionChanged;

            // ArtistsInVideoの変更を監視
            ArtistsInVideo.CollectionChanged += ArtistsInVideo_CollectionChanged;
        }

        public VideoItem( int id, string filePath, string fileName, long fileSize, DateTime lastModified, double duration ) {
            // VideoTagItemsの変更を監視
            VideoTagItems.CollectionChanged += VideoTagItems_CollectionChanged;

            // ArtistsInVideoの変更を監視
            ArtistsInVideo.CollectionChanged += ArtistsInVideo_CollectionChanged;

            Id = id;
            FilePath = filePath;
            FileName = fileName;
            FileSize = fileSize;
            LastModified = lastModified;
            Duration = duration;
        }


        public int Id { get; set; } // データベースの主キー
        public int FenrirId { get; set; } // FenrirのファイルID
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
        public ObservableCollection<TagItem> VideoTagItems { get; } = new ObservableCollection<TagItem>();

        /// <summary>
        /// VideoTagItemsの中身が変更されたときに呼び出されるイベントハンドラ
        /// </summary>
        private void VideoTagItems_CollectionChanged( object? sender, NotifyCollectionChangedEventArgs e ) {
            // TagVideoItemの中身が変更されたら、TagVideoCountプロパティも変更されたことをUIに通知
            PropertyChanged?.Invoke( this, new PropertyChangedEventArgs( nameof( VideoTagItems ) ) );
        }

        // ファイルのアーティスト情報
        public ObservableCollection<ArtistItem> ArtistsInVideo { get; } = new ObservableCollection<ArtistItem>();

        /// <summary>
        /// VideoTagItemsの中身が変更されたときに呼び出されるイベントハンドラ
        /// </summary>
        private void ArtistsInVideo_CollectionChanged( object? sender, NotifyCollectionChangedEventArgs e ) {
            // TagVideoItemの中身が変更されたら、TagVideoCountプロパティも変更されたことをUIに通知
            PropertyChanged?.Invoke( this, new PropertyChangedEventArgs( nameof( ArtistsInVideo ) ) );
        }

        // アーティスト名を除いたファイル名
        private string _fileNameWithoutArtists = string.Empty;
        public string FileNameWithoutArtists {
            get => _fileNameWithoutArtists;
            set {
                if ( _fileNameWithoutArtists != value ) {
                    _fileNameWithoutArtists = value;
                    OnPropertyChanged( nameof( FileNameWithoutArtists ) );
                }
            }
        }

        // UIスレッドから呼び出されることを前提とした、非同期でのサムネイル画像読み込みメソッド
        public async Task LoadThumbnailImageAsync() {
            // 既に画像がある、または元データがない場合は何もしない
            if ( ThumbnailImage != null || Thumbnail == null || Thumbnail.Length == 0 )                 return;

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
