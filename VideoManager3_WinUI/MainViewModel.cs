using CommunityToolkit.Mvvm.Input;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
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
using static MediaToolkit.Model.Metadata;

// タグとファイルの対応の取り方が下手、、、

namespace VideoManager3_WinUI
{
    public class MainViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<VideoItem> Videos { get; } = new ObservableCollection<VideoItem>();

        public ObservableCollection<TagItem> TagItems { get; } = new();

        public ICommand AddFolderCommand { get; }   // フォルダを指定してファイルを読み込むコマンド
        public ICommand ToggleViewCommand { get; }  // ビュー切り替えコマンド（グリッドビューとリストビューの切り替え）
        public ICommand EditTagCommand { get; }     // タグ編集コマンド
        public ICommand UpdateVideoTagsCommand { get; } // 動画のタグ情報を更新するコマンド

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
                    // 選択アイテムが変わったらコマンドの実行可否を更新
                    //((RelayCommand)UpdateVideoTagsCommand).RaiseCanExecuteChanged();
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
            UpdateVideoTagsCommand = new RelayCommand<TagItem>(async (tag) => await UpdateVideoTagSelection(tag), (tag) => SelectedItem != null);

            // 動画とタグの初期読み込み
            _ = LoadTagsAsync();    // タグの読み込みを非同期で開始
            _ = LoadVideosFromDbAsync();    // 動画の読み込みを非同期で開始
        }

        // ★追加：タグのチェック状態が変更されたときに呼び出されるメソッド
        private async Task UpdateVideoTagSelection(TagItem? tag)
        {
            if (SelectedItem == null || tag == null) return;

            // チェック状態に応じてDBとViewModelを更新
            if (tag.IsChecked)
            {
                // タグが既に追加されていなければ追加
                if (!SelectedItem.VideoTagItems.Any(t => t.Id == tag.Id))
                {
                    await _databaseService.AddTagToVideoAsync(SelectedItem, tag);
                    SelectedItem.VideoTagItems.Add(tag);
                }
            }
            else
            {
                // タグが既に存在すれば削除
                var tagToRemove = SelectedItem.VideoTagItems.FirstOrDefault(t => t.Id == tag.Id);
                if (tagToRemove != null)
                {
                    await _databaseService.RemoveTagFromVideoAsync(SelectedItem, tagToRemove);
                    SelectedItem.VideoTagItems.Remove(tagToRemove);
                }
            }
        }

        // ★追加：フライアウトが開かれる直前に、選択中の動画に合わせてタグのチェック状態を更新する
        public void PrepareTagsForEditing()
        {
            if (SelectedItem == null) return;

            // すべてのタグを再帰的に取得
            var allTags = new List<TagItem>();
            void FlattenTags(IEnumerable<TagItem> tags)
            {
                foreach (var tag in tags)
                {
                    allTags.Add(tag);
                    FlattenTags(tag.Children);
                }
            }
            FlattenTags(TagItems);

            // 現在選択されている動画が持っているタグIDのリストを作成
            var checkedTagIds = new HashSet<int>(SelectedItem.VideoTagItems.Select(t => t.Id));

            // 全てのタグをループし、選択状態に応じてIsCheckedプロパティを設定
            foreach (var tag in allTags)
            {
                tag.IsChecked = checkedTagIds.Contains(tag.Id);
            }
        }

        // 動画データをデータベースから読み込み、サムネイルを非同期で取得する
        private async Task LoadVideosFromDbAsync()
        {
            Videos.Clear();

            var videosFromDb = await _databaseService.GetAllVideosAsync();
            foreach (VideoItem video in videosFromDb)
            {
                // 動画アイテムをコレクションに追加
                Videos.Add(video);

                // 動画のタグをデータベースから取得
                var tagsFromVideo = await _databaseService.GetTagsForVideoAsync( video );

                // 親子関係にあるタグをすべてフラットなリストにし、IDで検索できるよう辞書に変換
                var allTags = new List<TagItem>();
                void FlattenTags(IEnumerable<TagItem> tags)
                {
                    foreach (var tag in tags)
                    {
                        allTags.Add(tag);
                        FlattenTags(tag.Children);
                    }
                }
                FlattenTags(TagItems);
                var allTagsLookup = allTags.ToDictionary(t => t.Id);

                // データベースから取得したタグに対応するViewModelのTagItemインスタンスを動画に追加
                foreach (var tagFromDb in tagsFromVideo)
                {
                    if (allTagsLookup.TryGetValue(tagFromDb.Id, out var existingTag))
                    {
                        video.VideoTagItems.Add(existingTag);
                    }
                }

                _ = Task.Run(() => LoadThumbnailAsync(video));
            }
        }

        // 動画のサムネイルを非同期で読み込み、UIスレッドで設定する
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
                // データベースからすべてのタグを取得
                var allTags = await _databaseService.GetTagsAsync();
                var tagDict = allTags.ToDictionary(t => t.Id);

                var rootTags = new List<TagItem>();

                // 1. タグの階層を構築
                foreach (var tag in allTags)
                {
                    if (   tag.ParentId.HasValue && tag.ParentId != 0
                        && tagDict.TryGetValue(tag.ParentId.Value, out var parentTag))
                    {
                        parentTag.Children.Add(tag);
                    }
                    else
                    {
                        rootTags.Add(tag);
                    }
                }

                // 2. 子タグとルートタグを順序でソート
                foreach (var tag in allTags.Where(t => t.Children.Any()))
                {
                    var sortedChildren = tag.Children.OrderBy(c => c.OrderInGroup).ToList();
                    tag.Children.Clear();
                    sortedChildren.ForEach(tag.Children.Add);
                }
                var sortedRootTags = rootTags.OrderBy(t => t.OrderInGroup).ToList();

                // 3. UIスレッドでUIを更新
                // UIのタグツリーを更新
                TagItems.Clear();
                sortedRootTags.ForEach(TagItems.Add);

                // 各動画アイテムが持つタグのインスタンスを最新のものに更新
                foreach (var video in Videos)
                {
                    var currentTagIds = video.VideoTagItems.Select(t => t.Id).ToList();
                    video.VideoTagItems.Clear();
                    foreach (var tagId in currentTagIds)
                    {
                        if (tagDict.TryGetValue(tagId, out var updatedTag))
                        {
                            video.VideoTagItems.Add(updatedTag);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading tags from database: {ex.Message}");
            }
        }

        // タグ編集コマンド
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
                Title = "タグの編集",
                Content = inputTextBox,
                PrimaryButtonText = "OK",
                CloseButtonText = "キャンセル",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = App.MainWindow.Content.XamlRoot // ダイアログを表示するために必要
            };

            var result = await dialog.ShowAsync();

            // OKボタンが押され、かつテキストが空でなく、変更があった場合のみ更新
            if (   result == ContentDialogResult.Primary
                && !string.IsNullOrWhiteSpace(inputTextBox.Text)
                && SelectedTag.Name != inputTextBox.Text)
            {
                SelectedTag.Name = inputTextBox.Text; // ViewModelのプロパティを更新
                await _databaseService.AddOrUpdateTagAsync(SelectedTag); // データベースを更新
                await LoadTagsAsync(); // タグツリーを再読み込みしてUIを更新
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
    }
}
