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
        public int FenrirId { get; set; } = 0; // FenrirのファイルID

        private string? _fileName;
        /// <summary>
        /// ファイル名 (例: "video.mp4")
        /// </summary>
        public string? FileName {
            get => _fileName;
            set {
                if ( _fileName != value ) {
                    _fileName = value;
                    OnPropertyChanged( nameof( FileName ) );
                }
            }
        }

        private string? _filePath;
        /// <summary>
        /// ファイルのフルパス
        /// </summary>
        public string? FilePath {
            get => _filePath;
            set {
                if ( _filePath != value ) {
                    _filePath = value;
                    OnPropertyChanged( nameof( FilePath ) );
                }
            }
        }

        private string? _extension;
        /// <summary>
        /// ファイルの拡張子 (例: ".mp4")
        /// </summary>
        public string? Extension {
            get => _extension;
            set {
                if ( _extension != value ) {
                    _extension = value;
                    OnPropertyChanged( nameof( Extension ) );
                }
            }
        }

        private long _fileSize;
        /// <summary>
        /// ファイルサイズ（バイト単位）
        /// </summary>
        public long FileSize {
            get => _fileSize;
            set {
                if ( _fileSize != value ) {
                    _fileSize = value;
                    OnPropertyChanged( nameof( FileSize ) );
                }
            }
        }

        private DateTime _lastModified;
        /// <summary>
        /// 最終更新日時（YYYY/MM/dd HH:mm:ss形式）
        /// </summary>
        public DateTime LastModified {
            get => _lastModified;
            set {
                if ( _lastModified != value ) {
                    _lastModified = value;
                    OnPropertyChanged( nameof( LastModified ) );
                }
            }
        }

        private double _duration;
        /// <summary>
        /// 動画の再生時間（秒）
        /// </summary>
        public double Duration {
            get => _duration;
            set {
                if ( _duration != value ) {
                    _duration = value;
                    OnPropertyChanged( nameof( Duration ) );
                }
            }
        }

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

        private byte[]? _thumbnail;
        /// <summary>
        /// サムネイルの元データ（軽量なbyte配列）
        /// </summary>
        public byte[]? Thumbnail {
            get => _thumbnail;
            set {
                if ( _thumbnail != value ) {
                    _thumbnail = value;
                    OnPropertyChanged( nameof( Thumbnail ) );
                }
            }
        }

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
            if ( ThumbnailImage != null || Thumbnail == null || Thumbnail.Length == 0 )
                return;

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

        // サムネイル画像を強制的にリロードする
        public async Task ReloadThumbnailImageAsync() {
            // 一度nullにしてから再読み込み
            ThumbnailImage = null;
            await LoadThumbnailImageAsync();
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
