using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;

namespace VideoManager3_WinUI
{
    public class MainViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<VideoItem> Videos { get; } = new ObservableCollection<VideoItem>();
        public ICommand AddFolderCommand { get; }
        public ICommand ToggleViewCommand { get; }
        
        private bool _isGridView = true;
        public bool IsGridView
        {
            get => _isGridView;
            set
            {
                if (_isGridView != value)
                {
                    _isGridView = value;
                    OnPropertyChanged(nameof(IsGridView));
                    OnPropertyChanged(nameof(IsListView));
                }
            }
        }
        public bool IsListView => !_isGridView;

        // 選択されたアイテムを保持するプロパティを追加
        private VideoItem? _selectedItem;
        public VideoItem? SelectedItem
        {
            get => _selectedItem;
            set
            {
                if (_selectedItem != value)
                {
                    _selectedItem = value;
                    OnPropertyChanged(nameof(SelectedItem));
                }
            }
        }

        private readonly DispatcherQueue _dispatcherQueue;
        private readonly ThumbnailService _thumbnailService;
        private readonly DatabaseService _databaseService;

        public MainViewModel(DispatcherQueue dispatcherQueue)
        {
            _dispatcherQueue = dispatcherQueue;
            _thumbnailService = new ThumbnailService();

            var dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VideoManager3", "videos.db");
            Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
            _databaseService = new DatabaseService(dbPath);

            AddFolderCommand = new RelayCommand(async () => await AddFolder());
            ToggleViewCommand = new RelayCommand(ToggleView);
            
            _ = LoadVideosFromDbAsync();
        }

        private async Task LoadVideosFromDbAsync()
        {
            var videosFromDb = await _databaseService.GetAllVideosAsync();
            foreach (var video in videosFromDb)
            {
                Videos.Add(video);
                _ = Task.Run(() => LoadThumbnailAsync(video));
            }
        }

        private async Task AddFolder()
        {
            var folderPicker = new FolderPicker
            {
                SuggestedStartLocation = PickerLocationId.VideosLibrary
            };
            folderPicker.FileTypeFilter.Add("*");

            if (App.m_window == null) return;
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.m_window);
            WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, hwnd);

            StorageFolder folder = await folderPicker.PickSingleFolderAsync();
            if (folder != null)
            {
                var files = await folder.GetFilesAsync();
                foreach (var file in files)
                {
                    if (file.FileType.ToLower() == ".mp4")
                    {
                        bool exists = false;
                        foreach (var v in Videos)
                        {
                            if (v.FilePath == file.Path)
                            {
                                exists = true;
                                break;
                            }
                        }

                        if (!exists)
                        {
                            var videoItem = new VideoItem(file.Path);
                            await _databaseService.AddVideoAsync(videoItem);
                            Videos.Add(videoItem);
                            _ = Task.Run(() => LoadThumbnailAsync(videoItem));
                        }
                    }
                }
            }
        }

        private async Task LoadThumbnailAsync(VideoItem videoItem)
        {
            var imageBytes = await _thumbnailService.GetThumbnailBytesAsync(videoItem.FilePath);

            if (imageBytes != null && imageBytes.Length > 0)
            {
                _dispatcherQueue.TryEnqueue(async () =>
                {
                    try
                    {
                        var bitmapImage = new BitmapImage();
                        using var stream = new InMemoryRandomAccessStream();
                        await stream.WriteAsync(imageBytes.AsBuffer());
                        stream.Seek(0);
                        await bitmapImage.SetSourceAsync(stream);

                        videoItem.Thumbnail = bitmapImage;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to set thumbnail source on UI thread for {videoItem.FileName}: {ex.Message}");
                    }
                });
            }
        }

        private void ToggleView()
        {
            IsGridView = !IsGridView;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}