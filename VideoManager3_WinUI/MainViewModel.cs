using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.Storage.Pickers;

// タグとファイルの対応の取り方が下手、、、

namespace VideoManager3_WinUI {
    public class MainViewModel:INotifyPropertyChanged {
        public ObservableCollection<VideoItem> Videos => _videoService.Videos;

        public ObservableCollection<TagItem> TagItems => _tagService.TagItems;

        public ObservableCollection<ArtistItem> ArtistItems => _artistService.Artists;

        // 表示用のコレクション
        public ObservableCollection<VideoItem> FilteredVideos { get; } = new ObservableCollection<VideoItem>();

        public ICommand AddFolderCommand { get; private set; }
        public ICommand AddFilesCommand { get; private set; }
        public ICommand ToggleViewCommand { get; private set; }  // ビュー切り替えコマンド（グリッドビューとリストビューの切り替え）
        public IRelayCommand EditTagCommand { get; private set; }     // タグ編集コマンド
        public IRelayCommand UpdateVideoTagsCommand { get; private set; } // 動画のタグ情報を更新するコマンド
        public IRelayCommand DoubleTappedCommand { get; private set; }    // ファイルをダブルクリックしたときのコマンド
        public ICommand SetHomeFolderCommand { get; private set; } // ホームフォルダを設定するコマンド
        public IRelayCommand DeleteFileCommand { get; private set; } // ファイルを削除するコマンド（未実装）W

        // 選択されたファイルアイテムを保持するプロパティ
        private VideoItem? _selectedItem;
        public VideoItem? SelectedItem {
            get => _selectedItem;
            set {
                if ( _selectedItem != value ) {
                    // 古いSelectedItemのPropertyChangedイベントの購読を解除
                    if ( _selectedItem != null ) {
                        _selectedItem.PropertyChanged -= SelectedItem_PropertyChanged;
                    }

                    _selectedItem = value;
                    OnPropertyChanged( nameof( SelectedItem ) );
                    // 選択アイテムが変わったらコマンドの実行可否を更新
                    DoubleTappedCommand.NotifyCanExecuteChanged();
                    UpdateVideoTagsCommand.NotifyCanExecuteChanged();

                    // 新しいSelectedItemのPropertyChangedイベントを購読
                    if ( _selectedItem != null ) {
                        _selectedItem.PropertyChanged += SelectedItem_PropertyChanged;
                    }
                }
            }
        }

        // TreeViewで選択されているタグを保持するためのプロパティ
        private TagItem? _selectedTag;
        public TagItem? SelectedTag {
            get => _selectedTag;
            set {
                if ( _selectedTag != value && !IsTagSetting ) {
                    _selectedTag = value;
                    OnPropertyChanged( nameof( SelectedTag ) );
                    if ( _selectedTag != null && _selectedTag.Name.Equals( "全てのファイル" ) )
                        SelectedArtist = null; // 「全てのファイル」が選択された場合はアーティスト選択を解除
                    EditTagCommand.NotifyCanExecuteChanged();
                    FilterVideos(); // タグが変更されたらフィルタリングを実行
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
                    FilterVideos(); // アーティストが変更されたらフィルタリングを実行
                }
            }
        }

        // タグ設定中かどうかを示すプロパティ
        private bool _isTagSetting = false;
        public bool IsTagSetting {
            get => _isTagSetting;
            set {
                _isTagSetting = value;
                // タグ設定モードの切り替えに応じて、すべてのタグのIsEditingプロパティを更新
                var tmpTag = _tagService.GetTagsInOrder();
                foreach ( var tag in tmpTag ) {
                    tag.IsEditing = value;
                }
            }
        }

        // （サムネイル表示 ←→ リストビュー表示）を切り替るプロパティ
        private bool _isGridView = true;
        public bool IsGridView {
            get => _isGridView;
            set {
                if ( _isGridView != value ) {
                    _isGridView = value;
                    OnPropertyChanged( nameof( IsGridView ) );
                    OnPropertyChanged( nameof( IsListView ) );
                }
            }
        }
        public bool IsListView => !_isGridView;

        // （タグツリービュー ←→ アーティスト一覧）の表示を切り替えるプロパティ
        private bool _isTreeView = true;
        public bool IsTreeView {
            get => _isTreeView;
            set {
                if ( _isTreeView != value ) {
                    _isTreeView = value;
                    OnPropertyChanged( nameof( IsTreeView ) );
                    OnPropertyChanged( nameof( IsArtistView ) );
                }
            }
        }
        public bool IsArtistView => !_isTreeView;

        // スライダーの値を保持し、サムネイルサイズを制御するためのプロパティ（サムネイルの横幅）
        private double _thumbnailSize;
        public double ThumbnailSize {
            get => _thumbnailSize;
            set {
                if ( _thumbnailSize != value ) {
                    _thumbnailSize = value;
                    OnPropertyChanged( nameof( ThumbnailSize ) );
                    OnPropertyChanged( nameof( ThumbnailHeight ) );
                }
            }
        }
        public double ThumbnailHeight => ThumbnailSize * 9.0 / 16.0;    // サムネイルの高さ

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

        // 検索テキストを保持するプロパティ
        private string _searchText = "";
        public string SearchText {
            get => _searchText;
            set {
                if ( _searchText != value ) {
                    _searchText = value;
                    OnPropertyChanged( nameof( SearchText ) );
                    FilterVideos(); // 検索テキストが変更されたらフィルタリングを実行
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
                    FilterVideos();
                }
            }
        }

        private readonly DatabaseService _databaseService;
        private readonly VideoService _videoService;
        private readonly TagService _tagService;
        private readonly ArtistService _artistService;

        // コンストラクタ
        public MainViewModel() {
            var dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VideoManager3", "videos.db");
            Directory.CreateDirectory( Path.GetDirectoryName( dbPath )! );
            _databaseService = new DatabaseService( dbPath );
            _tagService = new TagService( _databaseService );
            _videoService = new VideoService( _databaseService, _tagService, new ThumbnailService() );
            _artistService = new ArtistService( _databaseService );

            // コマンドの初期化
            AddFolderCommand = new CommunityToolkit.Mvvm.Input.RelayCommand( async () => { await _videoService.AddVideosFromFolderAsync(); FilterVideos(); } );
            AddFilesCommand = new RelayCommand<IEnumerable<string>>( async ( files ) => { await _videoService.AddVideosFromPathsAsync( files ); _videoService.SortVideos( SortType ); FilterVideos(); } );
            DeleteFileCommand = new RelayCommand( async () => { await _videoService.DeleteVideoAsync( SelectedItem ); FilterVideos(); }, () => SelectedItem != null );
            ToggleViewCommand = new CommunityToolkit.Mvvm.Input.RelayCommand( ToggleView );
            EditTagCommand = new CommunityToolkit.Mvvm.Input.RelayCommand<TagItem>( async ( tag ) => await EditTagAsync( tag ) );
            UpdateVideoTagsCommand = new RelayCommand<VideoItem>( async ( video ) => await UpdateVideoTagSelection( video ) );
            DoubleTappedCommand = new CommunityToolkit.Mvvm.Input.RelayCommand( () => _videoService.OpenFile( SelectedItem ), () => SelectedItem != null );
            SetHomeFolderCommand = new CommunityToolkit.Mvvm.Input.RelayCommand( async () => await SetHomeFolderAsync() );

            // 動画とタグの初期読み込み
            _ = LoadInitialDataAsync();
        }

        // 初期データをDBからロード
        private async Task LoadInitialDataAsync() {
            // タグと動画の初期データをロード
            await _videoService.LoadVideosAsync();    // 動画の読み込みを非同期で開始
            await _tagService.LoadTagsAsync();    // タグの読み込みを非同期で開始
            await _videoService.LoadVideoTagsAsync(); // 動画のタグ情報を非同期で読み込み
            await _tagService.LoadTagVideos( _videoService ); // タグに動画を関連付ける
            await _artistService.LoadArtistsAsync(); // アーティスト情報を非同期で読み込み
            await _artistService.LoadArtistVideosAsync( Videos ); // アーティストに動画を関連付ける

            // ファイルをソート
            _videoService.SortVideos( SortType );
            FilterVideos();
        }

        /// <summary>
        /// ファイルのフィルタリングを行う（タグ、検索テキスト）
        /// </summary>
        private void FilterVideos() {
            FilteredVideos.Clear();
            IEnumerable<VideoItem> targetVideos = Videos;

            // タグによるフィルタリング
            IEnumerable<VideoItem> videosByTag;
            if ( SelectedTag == null || SelectedTag.Name.Equals( "全てのファイル" ) ) {
                // タグが選択されていない、または「全てのファイル」が選択されている場合は、全動画を対象
                videosByTag = targetVideos;
            } else if ( SelectedTag.Name.Equals( "タグなし" ) ) {
                // 「タグなし」だった場合は、タグが紐づいていない動画を全て対象
                videosByTag = targetVideos.Where( v => !v.VideoTagItems.Any() );
            } else {
                // 選択されたタグに紐づく動画を対象
                var tagIds = new HashSet<int>(SelectedTag.GetAllDescendantIds());
                videosByTag = targetVideos.Where( v => v.VideoTagItems.Any( t => tagIds.Contains( t.Id ) ) );
            }
            targetVideos = videosByTag;

            // アーティストによるフィルタリング
            IEnumerable<VideoItem> videosByArtist;
            if ( SelectedArtist == null ) {
                // アーティストが選択されていない場合は、全動画を対象
                videosByArtist = targetVideos;
            } else {
                // 選択されたアーティストに紐づく動画を対象
                videosByArtist = targetVideos.Where( v => v.ArtistsInVideo.Any( a => a.Id == SelectedArtist.Id ) );
            }
            targetVideos = videosByArtist;

            // 検索テキストによるフィルタリング
            IEnumerable<VideoItem> videosBySerach;
            if ( string.IsNullOrWhiteSpace( SearchText ) ) {
                // 検索テキストが空の場合は、タグでフィルタリングされた結果をそのまま表示
                videosBySerach = targetVideos;
            } else {
                // 検索テキストを半角スペースで分割し、AND検索
                var searchKeywords = SearchText.ToLower().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                videosBySerach = targetVideos.Where( v => {
                    if ( string.IsNullOrEmpty( v.FileName ) )
                        return false;
                    var fileNameLower = v.FileName.ToLower();
                    return searchKeywords.All( keyword => fileNameLower.Contains( keyword ) );
                } );
            }
            targetVideos = videosBySerach;

            foreach ( var video in targetVideos ) {
                FilteredVideos.Add( video );
            }

            // 絞り込み後の先頭の動画を選択状態にする
            if ( FilteredVideos.Count > 0 ) {
                SelectedItem = FilteredVideos[0];
            } else {
                SelectedItem = null; // 絞り込み結果がない場合は選択を解除
            }
        }

        // ファイルに対するタグ設定ボタン
        private async Task UpdateVideoTagSelection( VideoItem? targetItem ) {
            if ( targetItem == null )
                return;

            var tmpTag = _tagService.GetTagsInOrder();
            foreach ( var tag in tmpTag ) {
                // チェック状態に応じてDBとViewModelを更新
                if ( tag.IsChecked ) {
                    // タグが既に追加されていなければ追加
                    if ( !targetItem.VideoTagItems.Any( t => t.Id == tag.Id ) ) {
                        await _databaseService.AddTagToVideoAsync( targetItem, tag );
                        targetItem.VideoTagItems.Add( tag );
                        tag.TagVideoItem.Add( targetItem ); // タグ側の関連付けも更新
                    }
                } else {
                    // タグが既に存在すれば削除
                    var tagToRemove = targetItem.VideoTagItems.FirstOrDefault(t => t.Id == tag.Id);
                    if ( tagToRemove != null ) {
                        await _databaseService.RemoveTagFromVideoAsync( targetItem, tagToRemove );
                        targetItem.VideoTagItems.Remove( tagToRemove );
                        tag.TagVideoItem.Remove( targetItem ); // タグ側の関連付けも更新
                    }
                }
                //await _tagService.LoadTagVideos( _videoService );
                //FilterVideos();
            }
        }

        // ファイルに対するタグ設定ボタン
        // フライアウトが開かれる直前に、選択中の動画に合わせてタグのチェック状態を更新する
        public void PrepareTagsForEditing() {
            if ( SelectedItem == null )
                return;

            // すべてのタグを再帰的に取得
            var allTags = new List<TagItem>();
            void FlattenTags( IEnumerable<TagItem> tags ) {
                foreach ( var tag in tags ) {
                    allTags.Add( tag );
                    FlattenTags( tag.Children );
                }
            }
            FlattenTags( TagItems );

            // 現在選択されている動画が持っているタグIDのリストを作成
            var checkedTagIds = new HashSet<int>(SelectedItem.VideoTagItems.Select(t => t.Id));

            // 全てのタグをループし、選択状態に応じてIsCheckedプロパティを設定
            foreach ( var tag in allTags ) {
                tag.IsChecked = checkedTagIds.Contains( tag.Id );
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
            if ( folder != null ) {
                HomeFolderPath = folder.Path; // 選択されたフォルダのパスを設定
            }
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
                    if ( tag.IsModified ) {
                        await _tagService.AddOrUpdateTagAsync( tag );
                    }
                    if ( tag.Children.Any() ) {
                        await SaveTagsInClose( tag.Children );
                    }
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

        // ファイルの表示方法を切り替える（一覧表示 ←→ サムネイル ）
        private void ToggleView() {
            IsGridView = !IsGridView;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged( string propertyName ) {
            PropertyChanged?.Invoke( this, new PropertyChangedEventArgs( propertyName ) );
        }

        // ファイル名を変更するメソッド
        public async Task RenameFileAsync( string newFileName ) {
            if ( SelectedItem == null || SelectedItem.FileName == null || SelectedItem.FileName.Equals( newFileName )) {
                return;
            }

            // 変更前のアーティスト名を取得
            string oldArtists = ArtistService.GetArtistNameWithoutFileName( SelectedItem.FileName );

            // アーティスト名を除いたファイル名を取得
            string newFileNameWithoutArtists = ArtistService.GetFileNameWithoutArtist( newFileName );
            await _videoService.RenameFileAsync( SelectedItem, newFileName, newFileNameWithoutArtists );

            string newArtists = ArtistService.GetArtistNameWithoutFileName( newFileName );
            if ( !(String.IsNullOrEmpty( newArtists ) || newArtists.Equals( oldArtists )) ) {
                // ファイル名に含まれるアーティスト名が変更された場合、アーティスト情報を更新
                // 新しい名前でアーティスト情報を更新
                await _artistService.AddOrUpdateArtistFromVideoAsync( SelectedItem );
            }

            // 変更後のファイル名でソート
            _videoService.SortVideos( SortType );
            //FilterVideos();
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