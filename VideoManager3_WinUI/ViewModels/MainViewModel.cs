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

namespace VideoManager3_WinUI.ViewModels {
    public class MainViewModel:INotifyPropertyChanged {
        public ObservableCollection<VideoItem> Videos => _videoService.Videos;
        public ObservableCollection<ArtistItem> ArtistItems => _artistService.Artists;

        // フィルター項目リスト（表示用）
        public ObservableCollection<FilterItem> Filters => _filterService.Filters;

        public Brush FilterButtonColor => _filterService.ButtonColor;
        public string FilterText => _filterService.ButtonText;

        // 絞り込み後の動画（表示用）
        public ObservableCollection<VideoItem> FilteredVideos { get; } = new ObservableCollection<VideoItem>();

        // ViewModelのインスタンス
        public UIManager UIManager { get; }
        public TagTreeViewModel TagTreeViewModel { get; }
        public ArtistViewModel ArtistViewModel { get; }

        // サービスのインスタンス
        private readonly DatabaseService _databaseService;
        private readonly VideoService _videoService;
        private readonly TagService _tagService;
        private readonly ArtistService _artistService;
        private readonly FilterService _filterService;

        // コマンド
        public ICommand AddFolderCommand { get; private set; }
        public ICommand AddFilesCommand { get; private set; }
        public ICommand ToggleViewCommand { get; private set; }
        public IRelayCommand ToggleFilterCommand { get; private set; }
        public IRelayCommand DoubleTappedCommand { get; private set; }
        public ICommand SetHomeFolderCommand { get; private set; }
        public IRelayCommand DeleteFileCommand { get; private set; }

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
            ArtistViewModel = new ArtistViewModel( _artistService );

            // 操作イベントの購読
            TagTreeViewModel.SelectedTagChanged += OnSelectedTagChanged;    // タグ選択
            ArtistViewModel.SelectedArtistChanged += OnSelectedArtistChanged; // アーティスト選択
            _filterService.FilterStateChanged += ApplyFilters;  // フィルターの有効/無効切り替え
            _filterService.PropertyChanged += FilterService_PropertyChanged;    // 複数選択ボタンの有効/無効切り替え

            // コマンドの初期化
            AddFolderCommand = new RelayCommand( async () => { await _videoService.AddVideosFromFolderAsync(); ApplyFilters(); } );
            AddFilesCommand = new RelayCommand<IEnumerable<string>>( async ( files ) => { await _videoService.AddVideosFromPathsAsync( files ); _videoService.SortVideos( SortType ); ApplyFilters(); } );
            DeleteFileCommand = new RelayCommand( async () => { await _videoService.DeleteVideoAsync( SelectedItem ); ApplyFilters(); }, () => SelectedItem != null );
            ToggleViewCommand = UIManager.ToggleViewCommand;
            ToggleFilterCommand = new RelayCommand( () => _filterService.ToggleFilterMulti() );
            DoubleTappedCommand = new RelayCommand<VideoItem>( ( video ) => _videoService.OpenFile( video ) );
            SetHomeFolderCommand = new RelayCommand( async () => await SetHomeFolderAsync() );

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
            await ArtistViewModel.LoadArtists();

            // 2. サービスを使って、ロードしたデータ同士を関連付ける
            var allTags = TagTreeViewModel.GetTagsInOrder();    // 全てのタグをフラットなリストとして取得
            await _tagService.LinkVideosAndTagsAsync( Videos, allTags );
            await _artistService.LoadArtistVideosAsync( Videos, ArtistItems );

            // 3. 最後にソートとフィルタリング
            _videoService.SortVideos( SortType );
            ApplyFilters();
        }

        /// <summary>
        /// タグを選択した場合のイベント
        /// </summary>
        private void OnSelectedTagChanged( TagItem? newSelected ) {
            if ( _filterService.SetTagFilter( newSelected ) ) {
                ApplyFilters();
            }
        }

        /// <summary>
        /// アーティストを選択した場合のイベント
        /// </summary>
        private void OnSelectedArtistChanged( ArtistItem? newSelected ) {
            if ( _filterService.SetArtistFilter( newSelected ) ) {
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
        }

        private void FilterService_PropertyChanged( object? sender, PropertyChangedEventArgs e ) {
            if ( e.PropertyName == nameof( FilterService.ButtonColor ) ) {
                OnPropertyChanged( nameof( FilterButtonColor ) );
            } else if ( e.PropertyName == nameof( FilterService.ButtonText ) ) {
                OnPropertyChanged( nameof( FilterText ) );
            }
        }

        // ホームフォルダを設定するコマンド
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
            if ( folder != null )
                HomeFolderPath = folder.Path; // 選択されたフォルダのパスを設定
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

        public event PropertyChangedEventHandler? PropertyChanged;

        public event Action<VideoItem>? ScrollToItemRequested;

        protected virtual void OnPropertyChanged( string propertyName ) {
            PropertyChanged?.Invoke( this, new PropertyChangedEventArgs( propertyName ) );
        }

        // ファイル名を変更するメソッド
        // 将来的にはVideoServiceに移動する
        public async Task RenameFileAsync( string newFileName ) {
            if ( SelectedItem == null || SelectedItem.FileName == null || SelectedItem.FileName.Equals( newFileName ) ) {
                return;
            }

            // 変更前のアーティスト名を取得
            string oldArtists = ArtistService.GetArtistNameWithoutFileName(SelectedItem.FileName);

            // 新しいファイル名（アーティスト名を除外）を取得
            string newFileNameWithoutArtists = ArtistService.GetFileNameWithoutArtist(newFileName);
            await _videoService.RenameFileAsync( SelectedItem, newFileName, newFileNameWithoutArtists );

            string newArtists = ArtistService.GetArtistNameWithoutFileName(newFileName);
            if ( !(string.IsNullOrEmpty( newArtists ) || newArtists.Equals( oldArtists )) ) {
                // ファイル名に含まれるアーティスト名が変更された場合、アーティスト情報を更新
                // 新しい名前でアーティスト情報を更新
                await _artistService.AddOrUpdateArtistFromVideoAsync( SelectedItem, ArtistItems );
            }

            // 変更後のファイル名でソート
            _videoService.SortVideos( SortType );
        }

        // SelectedItemのプロパティ変更イベントハンドラ
        private async void SelectedItem_PropertyChanged( object? sender, PropertyChangedEventArgs e ) {
            if ( SelectedItem != null ) {
                // データベースを更新
                await _databaseService.UpdateVideoAsync( SelectedItem );
            }
        }
    }
}