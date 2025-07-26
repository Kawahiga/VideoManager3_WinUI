using Microsoft.UI.Xaml.Media.Imaging;
using System.ComponentModel;
using System.IO;

namespace VideoManager3_WinUI
{
    public class VideoItem : INotifyPropertyChanged
    {
        public int Id { get; set; } // データベースの主キー
        public string FileName { get; }
        public string FilePath { get; }

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

        public VideoItem(string filePath)
        {
            FilePath = filePath;
            FileName = Path.GetFileName(filePath);
        }

        // Null許容参照型に対応
        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
