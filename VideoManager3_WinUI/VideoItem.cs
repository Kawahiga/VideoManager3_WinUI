using System;
using System.ComponentModel;

namespace VideoManager3_WinUI
{
    // VideoまたはFolderを表すデータモデル
    public class VideoItem : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private int _id;
        public int Id
        {
            get => _id;
            set { _id = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Id))); }
        }

        private string _name = "";
        public string Name
        {
            get => _name;
            set { _name = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name))); }
        }

        private string _path = "";
        public string Path
        {
            get => _path;
            set { _path = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Path))); }
        }

        private byte[]? _thumbnail;
        public byte[]? Thumbnail
        {
            get => _thumbnail;
            set { _thumbnail = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Thumbnail))); }
        }

        private DateTime _lastModified;
        public DateTime LastModified
        {
            get => _lastModified;
            set { _lastModified = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LastModified))); }
        }

        private string? _length;
        public string? Length
        {
            get => _length;
            set { _length = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Length))); }
        }

        private bool _isFolder;
        public bool IsFolder
        {
            get => _isFolder;
            set { _isFolder = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsFolder))); }
        }
    }
}

