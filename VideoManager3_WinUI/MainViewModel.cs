using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.UI.StartScreen;

namespace VideoManager3_WinUI
{
    public class MainViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<VideoItem> Videos { get; } = new ObservableCollection<VideoItem>();

        public ObservableCollection<TagItem> TagItems { get; } = new();

        public ICommand AddFolderCommand { get; }
        public ICommand ToggleViewCommand { get; }
        public ICommand EditTagCommand { get; }

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

        // 選択されたファイルアイテムを保持するプロパティ
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

        // TreeViewで選択されているタグを保持するためのプロパティ
        private TagItem? _selectedTag;
        public TagItem? SelectedTag
        {
            get => _selectedTag;
            set
            {
                _selectedTag = value;
                OnPropertyChanged(nameof(SelectedTag));
                // 選択状態が変わったら、コマンドの実行可能性を再評価するよう通知
                ((RelayCommand)EditTagCommand).RaiseCanExecuteChanged();
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

            // コマンドの初期化
            AddFolderCommand = new RelayCommand(async () => await AddFolder());
            ToggleViewCommand = new RelayCommand(ToggleView);
            EditTagCommand = new RelayCommand(async () => await EditTagAsync(), () => SelectedTag != null);

            //LoadDummyTags();
            _ = LoadTagsAsync();

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

        // データベースからタグを読み込み、階層を構築する
        private async Task LoadTagsAsync()
        {
            try
            {
                var allTags = await _databaseService.GetTagsAsync();
                var tagDict = allTags.ToDictionary(t => t.Id);

                foreach (var tag in allTags)
                {
                    if (tag.ParentId != 0 && tag.ParentId != null)
                    {
                        int parentId = (int)tag.ParentId.Value;
                        tagDict.TryGetValue(parentId, out TagItem? parentTag);
                        parentTag?.Children.Add(tag);
                    }
                    else
                    {
                        TagItems.Add(tag);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading tags from database: {ex.Message}");
                return;
            }
        }

        // タグ名を編集
        public async Task EditTagAsync()
        {
            if (SelectedTag == null) return;

            // 編集用のTextBoxを作成
            var inputTextBox = new TextBox
            {
                AcceptsReturn = false,
                Height = 32,
                Text = SelectedTag.Name, // 現在のタグ名を設定
                SelectionStart = SelectedTag.Name.Length // テキスト末尾にカーソルを配置
            };

            // ContentDialogを作成
            var dialog = new ContentDialog
            {
                Title = "タグ名の変更",
                Content = inputTextBox,
                PrimaryButtonText = "OK",
                CloseButtonText = "キャンセル",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = App.MainWindow.Content.XamlRoot // ダイアログを表示するために必要
            };

            var result = await dialog.ShowAsync();

            // OKボタンが押され、かつテキストが空でなく、変更があった場合のみ更新
            if (result == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(inputTextBox.Text) && SelectedTag.Name != inputTextBox.Text)
            {
                SelectedTag.Name = inputTextBox.Text; // ViewModelのプロパティを更新
                await _databaseService.AddOrUpdateTagAsync(SelectedTag); // データベースを更新
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
                            // 動画ファイルの基本プロパティとビデオプロパティを取得
                            int id = 0; // IDはデータベースで自動生成されるため、ここでは仮の値
                            string filePath = file.Path;
                            string fileName = file.Name;
                            long fileSize = (long)(await file.GetBasicPropertiesAsync()).Size;
                            DateTime lastMod = (await file.GetBasicPropertiesAsync()).DateModified.DateTime;
                            double duration = (await file.Properties.GetVideoPropertiesAsync()).Duration.TotalSeconds;

                            var videoItem = new VideoItem( id, filePath, fileName, fileSize, lastMod, duration);
                            await _databaseService.AddVideoAsync(videoItem);
                            Videos.Add(videoItem);
                            _ = Task.Run(() => LoadThumbnailAsync(videoItem));
                        }
                    }
                }
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

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        /*
        private void LoadDummyTags()
        {
            var group1 = new TagItem
            {
                Id = 0,
                Name = "ジャンル",
                Color = new SolidColorBrush(Colors.CornflowerBlue),
                ColorCode = "#6495ED",
                ParentId = null,
                OrderInGroup = 0,
                IsGroup = true,
                Children =
                {
                    new TagItem
                    {
                        Id = 0,
                        Name = "アクション",
                        Color = new SolidColorBrush(Colors.OrangeRed),
                        ColorCode = "#FF4500",
                        ParentId = 1,
                        OrderInGroup = 0,
                        IsGroup = false,
                    },
                    new TagItem
                    {
                        Id = 0,
                        Name = "コメディ",
                        Color = new SolidColorBrush(Colors.Gold),
                        ColorCode = "#FFD700",
                        ParentId = 1,
                        OrderInGroup = 1,
                        IsGroup = false,
                    },
                }
            };

            var group2 = new TagItem
            {
                Id = 0,
                Name = "製作者",
                Color = new SolidColorBrush(Colors.SeaGreen),
                ColorCode = "#2E8B57",
                ParentId = null,
                OrderInGroup = 1,
                IsGroup = true,
                Children =
                {
                    new TagItem
                    {
                        Id = 0,
                        Name = "スタジオA",
                        Color = new SolidColorBrush(Colors.LightGreen),
                        ColorCode = "#90EE90",
                        ParentId = 4,
                        OrderInGroup = 0,
                        IsGroup = true,
                        Children =
                        {
                            new TagItem {
                                Id = 0,
                                Name = "監督X",
                                Color = new SolidColorBrush(Colors.Turquoise),
                                ColorCode = "#40E0D0",
                                ParentId = 5,
                                OrderInGroup = 0,
                                IsGroup = false
                            },
                            new TagItem {
                                Id = 0,
                                Name = "監督Y",
                                Color = new SolidColorBrush(Colors.Turquoise),
                                ColorCode = "#40E0D0",
                                ParentId = 5,
                                OrderInGroup = 1,
                                IsGroup = false
                            }
                        }
                    },
                    new TagItem {
                        Id = 0,
                        Name = "スタジオB",
                        Color = new SolidColorBrush(Colors.LightGreen),
                        ColorCode = "#90EE90",
                        ParentId = 4,
                        OrderInGroup = 1,
                        IsGroup = true,
                    },
                }
            };

            var singleTag = new TagItem {
                Id = 0,
                Name = "お気に入り",
                Color = new SolidColorBrush(Colors.HotPink),
                ColorCode = "#FF69B4",
                ParentId = null,
                OrderInGroup = 2,
                IsGroup = false
            };

            TagItems.Add(group1);
            TagItems.Add(group2);
            TagItems.Add(singleTag);

            foreach ( var tag in TagItems ) {
                _databaseService.AddOrUpdateTagAsync(tag);
                foreach (var child in tag.Children)
                {
                    _databaseService.AddOrUpdateTagAsync(child);
                    foreach (var grandChild in child.Children)
                    {
                        _databaseService.AddOrUpdateTagAsync(grandChild);
                    }
                }
            }
        }
        */
    }
}