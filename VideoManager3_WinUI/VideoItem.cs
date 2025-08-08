using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Storage.Streams;

// エンハンス案
// 1.拡張子をDBに登録して、動画の種類を識別(動画の種類、ファイル/フォルダの区別)
// 2.ファイルの生存確認を定期的に行い、存在しないファイルは削除
// 3.ファイルの削除

namespace VideoManager3_WinUI {
    public class VideoItem:INotifyPropertyChanged {
        public int Id { get; set; } // データベースの主キー
        public string FileName { get; }
        public string FilePath { get; }
        public long FileSize { get; set; }  // ファイルサイズ（バイト単位）
        public DateTime LastModified { get; set; }  // 最終更新日時（YYYY/MM/dd HH:mm:ss形式）
        public double Duration { get; set; }    // 動画の再生時間（秒）

        // サムネイルの元データ（軽量なbyte配列）
        public byte[]? Thumbnail { get; set; }

        // UI表示用のサムネイル画像（BitmapImage）
        private BitmapImage? _thumbnailImage;
        public BitmapImage? ThumbnailImage {
            get => _thumbnailImage;
            private set // Viewからの直接の変更は禁止
            {
                if (_thumbnailImage != value)
                {
                    _thumbnailImage = value;
                    OnPropertyChanged(nameof(ThumbnailImage));
                }
            }
        }
        
        // ファイルに設定されたタグ情報
        private ObservableCollection<TagItem> videoTagItems = new ObservableCollection<TagItem>();
        public ObservableCollection<TagItem> VideoTagItems {
            get => videoTagItems;
            set
            {
                if ( videoTagItems != value ) {
                    videoTagItems = value;
                    OnPropertyChanged( nameof( VideoTagItems ) );
                }
            }
        }

        public VideoItem( int id, string filePath, string fileName, long fileSize, DateTime lastModified, double duration ) {
            Id = id;
            FilePath = filePath;
            FileName = fileName;
            FileSize = fileSize;
            LastModified = lastModified;
            Duration = duration;
        }

        // UIスレッドから呼び出されることを前提とした、非同期でのサムネイル画像読み込みメソッド
        public async Task LoadThumbnailImageAsync()
        {
            // 既に画像がある、または元データがない場合は何もしない
            if (ThumbnailImage != null || Thumbnail == null || Thumbnail.Length == 0)
            {
                return;
            }

            try
            {
                var bitmapImage = new BitmapImage();
                using var stream = new InMemoryRandomAccessStream();
                await stream.WriteAsync(Thumbnail.AsBuffer());
                stream.Seek(0);
                
                // このメソッドはUIスレッドで実行されるため、直接ソースを設定できる
                await bitmapImage.SetSourceAsync(stream);
                ThumbnailImage = bitmapImage;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to create BitmapImage for {FileName}: {ex.Message}");
            }
        }
        
        // 表示されなくなったアイテムのメモリを解放する
        public void UnloadThumbnailImage()
        {
            ThumbnailImage = null;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged( string propertyName ) {
            PropertyChanged?.Invoke( this, new PropertyChangedEventArgs( propertyName ) );
        }
    }
}
