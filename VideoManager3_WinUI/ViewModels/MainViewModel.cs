using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using VideoManager3_WinUI.Models;
using VideoManager3_WinUI.Services;
using Windows.Storage.Pickers;
using static MediaToolkit.Model.Metadata;

namespace VideoManager3_WinUI.ViewModels {
    public class MainViewModel:INotifyPropertyChanged {
        public UIManager UIManager { get; }
        public ObservableCollection<VideoItem> Videos => _videoService.Videos;
        public ObservableCollection<ArtistItem> ArtistItems => _artistService.Artists;

        // フィルター項目リスト（表示用）
        public ObservableCollection<FilterItem> Filters => _filterService.Filters;

        public Brush FilterButtonColor => _filterService.ButtonColor;
        public string FilterText => _filterService.ButtonText;

        // 絞り込み後の動画（表示用）
        public ObservableCollection<VideoItem> FilteredVideos { get; } = new ObservableCollection<VideoItem>();

        // サービスのインスタンス
        private readonly DatabaseService _databaseService;
        private readonly VideoService _videoService;
        private readonly TagService _tagService;
        private readonly ArtistService _artistService;
        private readonly FilterService _filterService;

        public TagTreeViewModel TagTreeViewModel { get; }

        // コマンド
        public ICommand ToggleViewCommand { get; private set; }
        public IRelayCommand ToggleFilterCommand { get; private set; }
        public IRelayCommand DoubleTappedCommand { get; private set; }
        public ICommand SetHomeFolderCommand { get; private set; }
        public IAsyncRelayCommand DeleteFileCommand { get; private set; }
        public IAsyncRelayCommand CleanupCommand { get; private set; }

        // 選択されたファイルアイテムを保持するプロパティ
        private VideoItem? _selectedItem;
        public VideoItem? SelectedItem {
            get => _selectedItem;
            set {
                if ( _selectedItem != value ) {
                    if ( _selectedItem != null )
                        _selectedItem.PropertyChanged -= SelectedItem_PropertyChanged;

                    _selectedItem = value;
                    OnPropertyChanged( nameof( SelectedItem ) );
                    DoubleTappedCommand.NotifyCanExecuteChanged();
                    DeleteFileCommand.NotifyCanExecuteChanged();

                    if ( _selectedItem != null ) {
                        _selectedItem.PropertyChanged += SelectedItem_PropertyChanged;
                        // 選択ファイルの位置までスクロール
                        ScrollToItemRequested?.Invoke( _selectedItem );
                    }
                }
            }
        }

        // アーティストツリーで選択されているアーティストを保持するためのプロパティ
        private ArtistItem? _selectedArtist;
        public ArtistItem? SelectedArtist {
            get => _selectedArtist;
            set {
                if ( _selectedArtist != value ) {
                    _selectedArtist = value;
                    OnPropertyChanged( nameof( SelectedArtist ) );

                    if ( _filterService.SetArtistFilter( _selectedArtist ) ) {
                        ApplyFilters();
                    }
                }
            }
        }

        // 検索テキストを保持するプロパティ
        private string _searchText = "";
        public string SearchText {
            get => _searchText;
            set {
                if ( _searchText != value ) {
                    _searchText = value;
                    OnPropertyChanged( nameof( SearchText ) );

                    if ( _filterService.SetSearchTextFilter( _searchText ) ) {
                        ApplyFilters();
                    }
                }
            }
        }

        // ホームフォルダのパスを保持するプロパティ
        private string _homeFolderPath = "";
        public string HomeFolderPath {
            get => _homeFolderPath;
            set {
                if ( _homeFolderPath != value ) {
                    _homeFolderPath = value;
                    OnPropertyChanged( nameof( HomeFolderPath ) );
                }
            }
        }

        // 動画のソート方法
        private VideoSortType _sortType;
        public VideoSortType SortType {
            get => _sortType;
            set {
                if ( _sortType != value ) {
                    _sortType = value;
                    OnPropertyChanged( nameof( SortType ) );
                    _videoService.SortVideos( _sortType );
                    ApplyFilters();
                }
            }
        }

        // コンストラクタ
        public MainViewModel() {
            UIManager = new UIManager();
            var dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VideoManager3", "videos.db");
            Directory.CreateDirectory( Path.GetDirectoryName( dbPath )! );

            // サービス層のインスタンスを作成
            _databaseService = new DatabaseService( dbPath );
            _tagService = new TagService( _databaseService );
            _videoService = new VideoService( _databaseService, _tagService, new ThumbnailService() );
            _artistService = new ArtistService( _databaseService );
            _filterService = new FilterService();

            // ViewModel層のインスタンスを作成
            TagTreeViewModel = new TagTreeViewModel( _tagService );

            // 操作イベントの購読
            TagTreeViewModel.SelectedTagChanged += OnSelectedTagChanged;    // タグ選択
            _filterService.FilterStateChanged += ApplyFilters;  // フィルターの有効/無効切り替え
            _filterService.PropertyChanged += FilterService_PropertyChanged;    // 複数選択ボタンの有効/無効切り替え

            // コマンドの初期化
            DeleteFileCommand = new AsyncRelayCommand( DeleteSelectedFileAsync, () => SelectedItem != null );
            ToggleViewCommand = UIManager.ToggleViewCommand;
            ToggleFilterCommand = new RelayCommand( () => _filterService.ToggleFilterMulti() );
            DoubleTappedCommand = new RelayCommand<VideoItem>( ( video ) => _videoService.OpenFile( video ) );
            SetHomeFolderCommand = new RelayCommand( async () => await SetHomeFolderAsync() );
            CleanupCommand = new AsyncRelayCommand( ExecuteCleanupAsync );

            // 動画とタグの初期読み込み
            _ = LoadInitialDataAsync();
        }

        /// <summary>
        /// 初期データをDBからロード
        /// </summary>
        private async Task LoadInitialDataAsync() {
            // 1. 各データを個別にロード
            await _videoService.LoadVideosAsync();
            await TagTreeViewModel.LoadTagsAsync();
            await _artistService.LoadArtistsAsync();

            // 2. サービスを使って、ロードしたデータ同士を関連付ける
            var allTags = TagTreeViewModel.GetTagsInOrder();    // 全てのタグをフラットなリストとして取得
            await _tagService.LinkVideosAndTagsAsync( Videos, allTags );
            await _artistService.LoadArtistVideosAsync( Videos );

            // 3. 最後にソートとフィルタリング
            _videoService.SortVideos( SortType );
            ApplyFilters();
        }

        /// <summary>
        /// タグを選択した場合のイベント
        /// 選択したタグをフィルターに設定する
        /// </summary>
        private void OnSelectedTagChanged( TagItem? newSelectedTag ) {
            if ( _filterService.SetTagFilter( newSelectedTag ) ) {
                ApplyFilters();
            }
        }

        /// <summary>
        /// フィルターを適用する
        /// </summary>
        private void ApplyFilters() {
            // 選択中のアイテムを覚えておく
            var previouslySelectedItem = SelectedItem;
            FilteredVideos.Clear();
            var filteredVideos = _filterService.ApplyFilters(Videos);

            foreach ( var video in filteredVideos ) {
                FilteredVideos.Add( video );
            }

            if ( previouslySelectedItem != null && FilteredVideos.Contains( previouslySelectedItem ) ) {
                // 元の選択中の動画を再選択する
                SelectedItem = previouslySelectedItem;
            } else {
                // 選択中の動画がフィルター後のリストに存在しない場合、最初の動画を選択する
                SelectedItem = FilteredVideos.FirstOrDefault();
            }
            UpdateFilteredCounts();
        }

        /// <summary>
        /// フィルタリングに合わせて動画数を変更
        /// </summary>
        private void UpdateFilteredCounts() {
            var allTags = TagTreeViewModel.GetTagsInOrder();

            // アーティストフィルターが有効な場合、フィルタリング後のタグ数にする
            if ( Filters.Any( f => f.Type == FilterType.Artist && f.IsActive == false ) ) {
                var tagMap = allTags.ToDictionary( t => t.Id );
                foreach ( var tag in allTags ) {
                    tag.TempFilteredCount = 0;
                }

                var filtered = _filterService.ApplyFilters( Videos, FilterType.Artist );
                foreach ( var video in filtered ) {
                    foreach ( var videoTag in video.VideoTagItems ) {
                        if ( tagMap.TryGetValue( videoTag.Id, out var tag ) ) {
                            tag.TempFilteredCount++;
                        }
                    }
                }
            } else {
                foreach ( var tag in allTags ) {
                    tag.TempFilteredCount = tag.TagVideoItem.Count;
                }
            }

            // タグフィルターが有効な場合、フィルタリング後のアーティスト数にする
            if ( Filters.Any( f => f.Type == FilterType.Tag && f.IsActive == false ) ) {
                var artistMap = ArtistItems.ToDictionary( a => a.Id );
                foreach ( var artist in ArtistItems ) {
                    artist.TempFilteredCount = 0;
                }

                var filtered = _filterService.ApplyFilters( Videos, FilterType.Tag );
                foreach ( var video in filtered ) {
                    foreach ( var artistInVideo in video.ArtistsInVideo ) {
                        if ( artistMap.TryGetValue( artistInVideo.Id, out var artist ) ) {
                            artist.TempFilteredCount++;
                        }
                    }
                }
            } else {
                foreach ( var artist in ArtistItems ) {
                    artist.TempFilteredCount = artist.VideoCount;
                }
            }
        }

        /// <summary>
        /// フィルターの複数選択モードの切り替え時
        /// </summary>
        private void FilterService_PropertyChanged( object? sender, PropertyChangedEventArgs e ) {
            if ( e.PropertyName == nameof( FilterService.ButtonColor ) ) {
                OnPropertyChanged( nameof( FilterButtonColor ) );
            } else if ( e.PropertyName == nameof( FilterService.ButtonText ) ) {
                OnPropertyChanged( nameof( FilterText ) );
            }
        }

        /// <summary>
        /// ホームフォルダを設定するコマンド
        /// ・ホームフォルダ内のファイルがすでにDBに登録されているかチェック
        /// ・ホームフォルダ内のファイルを所定のフォルダに移動しDBに登録
        /// </summary>
        private async Task SetHomeFolderAsync() {
            var folderPicker = new FolderPicker
            {
                CommitButtonText = "選択"
            };
            folderPicker.FileTypeFilter.Add( "*" ); // すべてのファイルを表示
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.m_window);
            WinRT.Interop.InitializeWithWindow.Initialize( folderPicker, hwnd );

            // フォルダを選択
            var folder = await folderPicker.PickSingleFolderAsync();
            if ( folder != null ) {
                HomeFolderPath = folder.Path; // 選択されたフォルダのパスを設定

                // ホームフォルダ内のファイルをDBに登録
                await AddFilesInHomeFolder( HomeFolderPath );
            }
        }

        /// <summary>
        /// 起動時のホームフォルダに対する処理
        /// ・ホームフォルダ内のファイルがすでにDBに登録されているかチェック
        /// ・ホームフォルダ内のファイルを所定のフォルダに移動しDBに登録
        /// </summary>
        public async Task HandleHomeFolderOnStartupAsync() {
            if ( !string.IsNullOrEmpty( HomeFolderPath ) && Directory.Exists( HomeFolderPath ) ) {
                // ホームフォルダ内のファイルをDBに登録
                await AddFilesInHomeFolder( HomeFolderPath );
            }
        }

        /// <summary>
        /// ホームフォルダに対する共通処理
        /// ・フォルダ内のファイルがすでにDBに登録されているかチェック
        /// ・ホームフォルダ内のファイルを所定のフォルダに移動しDBに登録
        /// </summary>
        private async Task AddFilesInHomeFolder( string homeFolder ) {
            // ホームフォルダ内のファイルがすでにDBに登録されているかチェック
            var dupFiles = _videoService.GetDuplicateVideosInFolder( homeFolder );
            // 重複があった場合はメッセージボックスで通知
            if ( dupFiles.Count > 0 ) {
                string message = string.Join( "\n", dupFiles.Select( v => v.FilePath ) );
                await UIManager.ShowMessageDialogAsync( "重複ファイルの警告", message );
            }

            // ホームフォルダ内のファイルを所定のフォルダに移動しDBに登録
            var newVideos = await _videoService.MoveVideosToDateFoldersAsync( HomeFolderPath );

            if ( newVideos.Any() ) {
                // 新しく追加された各ビデオについてアーティスト情報を更新
                foreach ( var video in newVideos ) {
                    await _artistService.AddOrUpdateArtistFromVideoAsync( video );
                }

                // ソート順を維持し、フィルターを再適用してUIを正しく更新します。
                _videoService.SortVideos( SortType );
                ApplyFilters();
            }

            // ディスクの空き容量をチェック
            try {
                var driveInfo = new DriveInfo(Path.GetPathRoot(homeFolder));
                const long threshold = 10L * 1024 * 1024 * 1024; // 10GB
                if ( driveInfo.AvailableFreeSpace < threshold ) {
                    var freeSpaceGB = driveInfo.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0);
                    await UIManager.ShowMessageDialogAsync( "ディスク容量の警告", $"ディスクの空き容量が少なくなっています。({freeSpaceGB:F2} GB残り)" );
                }
            } catch ( Exception ex ) {
                // ドライブ情報の取得に失敗した場合（例：ネットワークドライブなど）
                Debug.WriteLine( $"ディスク容量のチェックに失敗しました: {ex.Message}" );
            }
        }

        // アプリを閉じるときのイベント
        public async Task WindowCloseAsync() {
            await TagTreeViewModel.SaveTagsInClose();
            await SaveArtistsInClose( ArtistItems );
        }

        // アーティスト情報を保存
        public async Task SaveArtistsInClose( ObservableCollection<ArtistItem> artists ) {
            try {
                foreach ( var artist in artists ) {
                    await _databaseService.AddOrUpdateArtistAsync( artist );
                }
            } catch ( Exception ex ) {
                Debug.WriteLine( $"Error saving artists: {ex.Message}" );
            }
        }

        /// <summary>
        /// 不要なデータをクリーンアップします。
        /// - リンク切れのサムネイル
        /// - 動画が0件のアーティスト
        /// </summary>
        private async Task ExecuteCleanupAsync() {
            await _videoService.DeleteOrphanedThumbnailsAsync();
            await _artistService.DeleteOrphanedArtistsAsync();
            await UIManager.ShowMessageDialogAsync( "クリーンアップ完了", "クリーンアップ処理が完了しました。\n - リンク切れのサムネイル\r\n - 動画が0件のアーティスト\r\n" );
            // 【エンハンス案】
            // リンク切れファイルを検出
            // 削除した件数を表示する
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public event Action<VideoItem>? ScrollToItemRequested;

        protected virtual void OnPropertyChanged( string propertyName ) {
            PropertyChanged?.Invoke( this, new PropertyChangedEventArgs( propertyName ) );
        }

        // ファイル名を変更するメソッド
        public async Task RenameFileAsync( VideoItem video, string newFileName ) {
            if ( video == null || string.IsNullOrWhiteSpace( video.FileName ) ) {
                return;
            }

            // 変更前の情報を保持
            string oldFileName = video.FileName;
            string? oldFilePath = video.FilePath;
            string oldFileNameWithoutArtists = video.FileNameWithoutArtists;
            string oldArtists = ArtistService.GetArtistNameWithoutFileName(oldFileName);

            if ( oldFileName == newFileName || oldFileNameWithoutArtists == newFileName ) {
                // ファイル名が変更されていない場合、何もしない
                return;
            }

            // --- ファイル名の変更処理 ---
            string newFileNameWithoutArtists = ArtistService.GetFileNameWithoutArtist(newFileName);
            var result = await _videoService.RenameFileAsync( video, newFileName, newFileNameWithoutArtists );

            if ( result == RenameResult.Success ) {
                // --- アーティスト情報の更新 ---
                string newArtists = ArtistService.GetArtistNameWithoutFileName(newFileName);
                if ( oldArtists != newArtists ) {
                    // ファイル名に含まれるアーティスト名が変更された場合、アーティスト情報を更新
                    // これにより、アーティスト名がなくなった場合も情報がクリアされる
                    await _artistService.AddOrUpdateArtistFromVideoAsync( video );
                }

                // --- UIの更新 ---
                // ソート順を維持し、フィルターを再適用してUIを正しく更新します。
                _videoService.SortVideos( SortType );
                ApplyFilters();

            } else {
                // --- エラー処理 ---
                string? errorMessage = result switch
                {
                    RenameResult.AlreadyExists => "同じ名前のファイルが既に存在します。",
                    RenameResult.AccessDenied => "ファイルへのアクセスが拒否されました。\nアクセス権限を確認してください。",
                    RenameResult.FileInUse => "ファイルは他のプログラムで使用中のため、変更できません。",
                    RenameResult.InvalidName => "ファイル名に使用できない文字が含まれています。",
                    _ => "原因不明のエラーにより、ファイル名の変更に失敗しました。"
                };

                if ( errorMessage != null ) {
                    await UIManager.ShowMessageDialogAsync( "名前の変更エラー", errorMessage );
                }

                // 失敗した場合、ViewModelが持つVideoItemのプロパティを元に戻す
                video.FileName = oldFileName;
                video.FilePath = oldFilePath;
                video.FileNameWithoutArtists = oldFileNameWithoutArtists;
            }
        }

        // SelectedItemのプロパティ変更イベントハンドラ
        private async void SelectedItem_PropertyChanged( object? sender, PropertyChangedEventArgs e ) {
            if ( SelectedItem != null ) {
                // データベースを更新
                await _databaseService.UpdateVideoAsync( SelectedItem );
            }
        }

        /// <summary>
        /// 選択されているファイルを、確認ダイアログ表示後に削除します。
        /// </summary>
        private async Task DeleteSelectedFileAsync() {
            if ( SelectedItem == null )
                return;

            // 確認ダイアログを表示
            bool confirmed = await UIManager.ShowConfirmationDialogAsync(
                "削除の確認",
                $"「{SelectedItem.FileName}」\nを完全に削除しますか？\n\nこの操作は元に戻せません。",
                "削除",
                "キャンセル");

            if ( confirmed ) {
                bool success = await _videoService.DeleteVideoAsync(SelectedItem);
                if ( !success ) {
                    await UIManager.ShowMessageDialogAsync( "削除エラー", "ファイルの削除に失敗しました。ファイルが他のプログラムで使用されていないか、アクセス許可があるか確認してください。" );
                }
                ApplyFilters();
            }
        }
    }
}