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
        public UIManager UIManager { get; }
        public ObservableCollection<VideoItem> Videos => _videoService.Videos;
        public ObservableCollection<TagItem> TagItems => _tagService.TagItems;
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
        private readonly FilterService _filterService; // ★ FilterServiceを追加

        // コマンド
        public ICommand AddFolderCommand { get; private set; }
        public ICommand AddFilesCommand { get; private set; }
        public ICommand ToggleViewCommand { get; private set; }
        public IRelayCommand ToggleFilterCommand { get; private set; }
        public IRelayCommand EditTagCommand { get; private set; }
        public IRelayCommand UpdateVideoTagsCommand { get; private set; }
        public IRelayCommand DoubleTappedCommand { get; private set; }
        public ICommand SetHomeFolderCommand { get; private set; }
        public IRelayCommand DeleteFileCommand { get; private set; }

        // 選択されたファイルアイテムを保持するプロパティ
        private VideoItem? _selectedItem;
        public VideoItem? SelectedItem {
            get => _selectedItem;
            set {
                if ( _selectedItem != value ) {
                    if ( _selectedItem != null )                         _selectedItem.PropertyChanged -= SelectedItem_PropertyChanged;

                    _selectedItem = value;
                    OnPropertyChanged( nameof( SelectedItem ) );
                    DoubleTappedCommand.NotifyCanExecuteChanged();
                    UpdateVideoTagsCommand.NotifyCanExecuteChanged();

                    if ( _selectedItem != null )                         _selectedItem.PropertyChanged += SelectedItem_PropertyChanged;
                }
            }
        }

        // TreeViewで選択されているタグを保持するためのプロパティ
        private TagItem? _selectedTag;
        public TagItem? SelectedTag {
            get => _selectedTag;
            set {
                if ( _selectedTag != value && !UIManager.IsTagSetting ) {
                    _selectedTag = value;
                    OnPropertyChanged( nameof( SelectedTag ) );
                    EditTagCommand.NotifyCanExecuteChanged();

                    if ( _filterService.SetTagFilter( _selectedTag ) )                         ApplyFilters();
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

                    if ( _filterService.SetArtistFilter( _selectedArtist ) )                         ApplyFilters();
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

                    if ( _filterService.SetSearchTextFilter( _searchText ) )                         ApplyFilters();
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
            _databaseService = new DatabaseService( dbPath );
            _tagService = new TagService( _databaseService );
            _videoService = new VideoService( _databaseService, _tagService, new ThumbnailService() );
            _artistService = new ArtistService( _databaseService );
            _filterService = new FilterService();
            // FilterServiceのイベントを購読
            _filterService.FilterStateChanged += ApplyFilters;

            // コマンドの初期化
            AddFolderCommand = new RelayCommand( async () => { await _videoService.AddVideosFromFolderAsync(); ApplyFilters(); } );
            AddFilesCommand = new RelayCommand<IEnumerable<string>>( async ( files ) => { await _videoService.AddVideosFromPathsAsync( files ); _videoService.SortVideos( SortType ); ApplyFilters(); } );
            DeleteFileCommand = new RelayCommand( async () => { await _videoService.DeleteVideoAsync( SelectedItem ); ApplyFilters(); }, () => SelectedItem != null );
            ToggleViewCommand = UIManager.ToggleViewCommand;
            ToggleFilterCommand = new RelayCommand( () => _filterService.ToggleFilterMulti() );
            EditTagCommand = new RelayCommand<TagItem>( async ( tag ) => await EditTagAsync( tag ) );
            UpdateVideoTagsCommand = new RelayCommand<VideoItem>( async ( video ) => await UpdateVideoTagSelection( video ) );
            DoubleTappedCommand = new RelayCommand( () => _videoService.OpenFile( SelectedItem ), () => SelectedItem != null );
            SetHomeFolderCommand = new RelayCommand( async () => await SetHomeFolderAsync() );

            // 動画とタグの初期読み込み
            _ = LoadInitialDataAsync();
        }

        // 初期データをDBからロード
        private async Task LoadInitialDataAsync() {
            await _videoService.LoadVideosAsync();
            await _tagService.LoadTagsAsync();

            await _videoService.LoadVideoTagsAsync();
            await _tagService.LoadTagVideos( _videoService );
            await _artistService.LoadArtistsAsync();
            await _artistService.LoadArtistVideosAsync( Videos );

            _videoService.SortVideos( SortType );
            ApplyFilters();
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
                // 元の選択ファイルの位置までスクロール
                ScrollToItemRequested?.Invoke( previouslySelectedItem );
            } else {
                // 選択中の動画がフィルター後のリストに存在しない場合、最初の動画を選択する
                SelectedItem = FilteredVideos.FirstOrDefault();
            }
            OnPropertyChanged( nameof( FilterText ) );
            OnPropertyChanged( nameof( FilterButtonColor ) );
        }

        // ファイルに対するタグ設定ボタン
        // ここに実装すべきではない。将来的にはVideoServiceへ移管
        private async Task UpdateVideoTagSelection( VideoItem? targetItem ) {
            if ( targetItem == null )
                return;

            targetItem.VideoTagItems.Clear();
            var tmpTag = _tagService.GetTagsInOrder();
            foreach ( var tag in tmpTag ) {
                // チェック状態に応じてDBとViewModelを更新
                if ( tag.IsChecked ) {
                    // タグが既に追加されていなければ追加
                    targetItem.VideoTagItems.Add( tag );
                    if ( !targetItem.VideoTagItems.Any( t => t.Id == tag.Id ) ) {
                        await _databaseService.AddTagToVideoAsync( targetItem, tag );
                        tag.TagVideoItem.Add( targetItem ); // タグ側の関連付けも更新
                    }
                } else {
                    // タグが既に存在すれば削除
                    var tagToRemove = targetItem.VideoTagItems.FirstOrDefault(t => t.Id == tag.Id);
                    if ( tagToRemove != null ) {
                        await _databaseService.RemoveTagFromVideoAsync( targetItem, tagToRemove );
                        //targetItem.VideoTagItems.Remove( tagToRemove );
                        tag.TagVideoItem.Remove( targetItem ); // タグ側の関連付けも更新
                    }
                }
                // タグの編集モードを解除
                tag.IsEditing = false;
            }
        }

        // ファイルに対するタグ設定ボタン
        // フライアウトが開かれる直前に、選択中の動画に合わせてタグのチェック状態を更新する
        public void PrepareTagsForEditing() {
            if ( SelectedItem == null )
                return;

            // すべてのタグを再帰的に取得
            var allTags = _tagService.GetTagsInOrder();

            // 現在選択されている動画が持っているタグIDのリストを作成
            var checkedTagIds = new HashSet<int>(SelectedItem.VideoTagItems.Select(t => t.Id));

            // 全てのタグをループし、選択状態に応じてIsCheckedプロパティを設定
            foreach ( var tag in allTags ) {
                tag.IsChecked = checkedTagIds.Contains( tag.Id );
                tag.IsEditing = true; // 設定モードに設定
            }
        }

        // タグ編集コマンド
        public async Task EditTagAsync( TagItem? tag ) {
            if ( App.MainWindow == null || tag == null )
                return;

            // 編集用のTextBoxを作成
            var inputTextBox = new TextBox
            {
                AcceptsReturn = false,
                Height = 32,
                Text = tag.Name, // 現在のタグ名を設定
                SelectionStart = tag.Name.Length // テキスト末尾にカーソルを配置
            };

            // ContentDialogを作成
            var dialog = new ContentDialog
            {
                Title = "タグの編集",
                Content = inputTextBox,
                PrimaryButtonText = "OK",
                SecondaryButtonText = "削除",
                CloseButtonText = "キャンセル",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = App.MainWindow.Content.XamlRoot // ダイアログを表示するために必要
            };

            var result = await dialog.ShowAsync();

            if ( result == ContentDialogResult.Primary ) {
                // OKボタンが押された場合、タグ名を更新
                if ( !string.IsNullOrWhiteSpace( inputTextBox.Text ) && tag.Name != inputTextBox.Text ) {
                    tag.Name = inputTextBox.Text; // ViewModelのプロパティを更新
                    await _tagService.AddOrUpdateTagAsync( tag );
                }
            } else if ( result == ContentDialogResult.Secondary ) {
                // 削除ボタンが押された場合、タグを削除
                await _tagService.DeleteTagAsync( tag );
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
            if ( folder != null )                 HomeFolderPath = folder.Path; // 選択されたフォルダのパスを設定
        }

        // アプリを閉じるときのイベント
        public async Task WindowCloseAsync() {
            await SaveTagsInClose( TagItems );
            await SaveArtistsInClose( ArtistItems );
        }

        // タグ情報を保存
        private async Task SaveTagsInClose( ObservableCollection<TagItem> tags ) {
            try {
                ObservableCollection<TagItem> tempTags = new ObservableCollection<TagItem>(tags);
                foreach ( var tag in tempTags ) {
                    if ( tag.IsModified )                         await _tagService.AddOrUpdateTagAsync( tag );
                    if ( tag.Children.Any() )                         await SaveTagsInClose( tag.Children );
                }
            } catch ( Exception ex ) {
                Debug.WriteLine( $"Error saving tags: {ex.Message}" );
            }
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
            if ( SelectedItem == null || SelectedItem.FileName == null || SelectedItem.FileName.Equals( newFileName ) )                 return;

            // 変更前のアーティスト名を取得
            string oldArtists = ArtistService.GetArtistNameWithoutFileName(SelectedItem.FileName);

            // 新しいファイル名（アーティスト名を除外）を取得
            string newFileNameWithoutArtists = ArtistService.GetFileNameWithoutArtist(newFileName);
            await _videoService.RenameFileAsync( SelectedItem, newFileName, newFileNameWithoutArtists );

            string newArtists = ArtistService.GetArtistNameWithoutFileName(newFileName);
            if ( !(string.IsNullOrEmpty( newArtists ) || newArtists.Equals( oldArtists )) )                 // ファイル名に含まれるアーティスト名が変更された場合、アーティスト情報を更新
                // 新しい名前でアーティスト情報を更新
                await _artistService.AddOrUpdateArtistFromVideoAsync( SelectedItem );

            // 変更後のファイル名でソート
            _videoService.SortVideos( SortType );
            //ApplyFilters();
        }

        // SelectedItemのプロパティ変更イベントハンドラ
        private async void SelectedItem_PropertyChanged( object? sender, PropertyChangedEventArgs e ) {
            if ( SelectedItem != null )                 // データベースを更新
                await _databaseService.UpdateVideoAsync( SelectedItem );
        }
    }
}