using MediaToolkit;
using MediaToolkit.Model;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.ComponentModel;
using System.IO;
using Windows.Media.Playback;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.UI.StartScreen;

// エンハンス案
// 1.拡張子をDBに登録して、動画の種類を識別(動画の種類、ファイル/フォルダの区別)
// 2.ファイルの生存確認を定期的に行い、存在しないファイルは削除
// 3.ファイルの削除

namespace VideoManager3_WinUI
{
    public class VideoItem : INotifyPropertyChanged
    {
        public int Id { get; set; } // データベースの主キー
        public string FileName { get; }
        public string FilePath { get; }
        public long FileSize { get; set; }  // ファイルサイズ（バイト単位）
        public DateTime LastModified { get; set; }
        public double Duration { get; set; }    // 動画の再生時間（秒）

        // サムネイルは非同期で読み込まれるため、null許容にする
        private BitmapImage? _thumbnail;
        public BitmapImage? Thumbnail
        {
            get => _thumbnail;
            set
            {
                if (_thumbnail != value)
                {
                    _thumbnail = value;
                    OnPropertyChanged(nameof(Thumbnail));
                }
            }
        }

        public VideoItem( int id, string filePath, string fileName, long fileSize, DateTime lastModified, double duration )
        {
            Id = id;
            FilePath = filePath;
            FileName = fileName;
            FileSize = fileSize;
            LastModified = lastModified;
            Duration = duration;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
