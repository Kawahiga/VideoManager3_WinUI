using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace VideoManager3_WinUI
{
    public class MainViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        // Viewにダイアログ表示を依頼するためのコールバック関数
        public Func<Task<string?>>? ShowAddTagDialogAsync { get; set; }

        public ObservableCollection<VideoItem> VideoItems { get; set; }
        public ObservableCollection<TagItem> TagItems { get; set; }

        private VideoItem? _selectedItem;
        public VideoItem? SelectedItem
        {
            get => _selectedItem;
            set { if (Equals(_selectedItem, value)) return; _selectedItem = value; OnPropertyChanged(); }
        }
        
        private TagItem? _selectedTagItem;
        public TagItem? SelectedTagItem
        {
            get => _selectedTagItem;
            set { if (Equals(_selectedTagItem, value)) return; _selectedTagItem = value; OnPropertyChanged(); }
        }

        public ICommand AddFolderCommand { get; }
        public ICommand AddTagCommand { get; }

        private IntPtr _hWnd;

        public MainViewModel()
        {
            VideoItems = new ObservableCollection<VideoItem>();
            TagItems = new ObservableCollection<TagItem>();
            LoadDataFromDatabase();

            AddFolderCommand = new RelayCommand(async (param) => await ExecuteAddFolder());
            AddTagCommand = new RelayCommand(async (param) => await ExecuteAddTag());
        }

        // InitializeメソッドからXamlRootの受け取りを削除
        public void Initialize(IntPtr hWnd)
        {
            _hWnd = hWnd;
        }

        private async Task ExecuteAddFolder()
        {
            var folderPicker = new FolderPicker { SuggestedStartLocation = PickerLocationId.VideosLibrary };
            folderPicker.FileTypeFilter.Add("*");
            InitializeWithWindow.Initialize(folderPicker, _hWnd);

            StorageFolder folder = await folderPicker.PickSingleFolderAsync();
            if (folder != null)
            {
                await ScanAndAddVideos(folder);
                LoadDataFromDatabase();
            }
        }

        private async Task ExecuteAddTag()
        {
            // Viewに設定されたダイアログ表示用の関数を呼び出す
            if (ShowAddTagDialogAsync == null) return;

            var newTagName = await ShowAddTagDialogAsync();

            // 結果がnullでなければ(キャンセルされていなければ)DBに登録
            if (!string.IsNullOrEmpty(newTagName))
            {
                int? parentId = SelectedTagItem?.Id;
                App.Database.AddTag(newTagName, parentId);
                LoadDataFromDatabase();
            }
        }

        private async Task ScanAndAddVideos(StorageFolder rootFolder)
        {
            var videoItems = new ObservableCollection<VideoItem>();
            var subFolders = await rootFolder.GetFoldersAsync();
            foreach (var subFolder in subFolders)
            {
                videoItems.Add(new VideoItem { Name = subFolder.Name, Path = subFolder.Path, LastModified = subFolder.DateCreated.DateTime, IsFolder = true });
            }
            var files = await rootFolder.GetFilesAsync();
            foreach (var file in files.Where(f => f.FileType.Equals(".mp4", StringComparison.OrdinalIgnoreCase)))
            {
                var properties = await file.GetBasicPropertiesAsync();
                videoItems.Add(new VideoItem { Name = file.Name, Path = file.Path, LastModified = properties.DateModified.DateTime, IsFolder = false });
            }
            if (videoItems.Any())
            {
                App.Database.AddVideoItems(videoItems);
            }
        }

        private void LoadDataFromDatabase()
        {
            var videos = App.Database.GetVideos();
            VideoItems.Clear();
            foreach (var video in videos) { VideoItems.Add(video); }

            var tags = App.Database.GetTags();
            TagItems.Clear();
            foreach (var tag in tags) { TagItems.Add(tag); }
        }

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

