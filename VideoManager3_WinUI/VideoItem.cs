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

namespace VideoManager3_WinUI
{
    public class VideoItem : INotifyPropertyChanged
    {
        public int Id { get; set; } // データベースの主キー
        public string FileName { get; }
        public string FilePath { get; }
        public long FileSize { get; set; }
        public DateTime LastModified { get; set; }
        public double Duration { get; set; }

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

        public VideoItem( StorageFile file )
        {
            Id = 0;
            FilePath = file.Path;
            FileName = file.Name;
            FileSize = 0;
            LastModified = file.DateCreated.DateTime;
            Duration = 0;

            //FileSize = (await file.GetBasicPropertiesAsync()).Size;
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
