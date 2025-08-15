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

namespace VideoManager3_WinUI {
    public class MainViewModel:INotifyPropertyChanged {
        public ObservableCollection<VideoItem> Videos => _videoService.Videos;

        public ObservableCollection<TagItem> TagItems => _tagService.TagItems;

        // 表示用のコレクション
        public ObservableCollection<VideoItem> FilteredVideos { get; } = new ObservableCollection<VideoItem>();

        public ICommand AddFolderCommand { get; }
        public ICommand AddFilesCommand { get; }
        public ICommand ToggleViewCommand { get; }  // ビュー切り替えコマンド（グリッドビューとリストビューの切り替え）
        public IRelayCommand EditTagCommand { get; }     // タグ編集コマンド
        public IRelayCommand UpdateVideoTagsCommand { get; } // 動画のタグ情報を更新するコマンド
        public IRelayCommand DoubleTappedCommand { get; }    // ファイルをダブルクリックしたときのコマンド

        // 選択されたファイルアイテムを保持するプロパティ
        private VideoItem? _selectedItem;
        public VideoItem? SelectedItem {
            get => _selectedItem;
            set
            {
                if ( _selectedItem != value ) {
                    _selectedItem = value;
                    OnPropertyChanged( nameof( SelectedItem ) );
                    // 選択アイテムが変わったらコマンドの実行可否を更新
                    DoubleTappedCommand.NotifyCanExecuteChanged();
                    UpdateVideoTagsCommand.NotifyCanExecuteChanged();
                }
            }
        }

        // TreeViewで選択されているタグを保持するためのプロパティ
        private TagItem? _selectedTag;
        public TagItem? SelectedTag {
            get => _selectedTag;
            set
            {
                if ( _selectedTag != value ) {
                    _selectedTag = value;
                    OnPropertyChanged( nameof( SelectedTag ) );
                    EditTagCommand.NotifyCanExecuteChanged();
                    FilterVideos();
                }
            }
        }

        // ビューの切り替え状態を保持するプロパティ
        private bool _isGridView = true;
        public bool IsGridView {
            get => _isGridView;
            set
            {
                if ( _isGridView != value ) {
                    _isGridView = value;
                    OnPropertyChanged( nameof( IsGridView ) );
                    OnPropertyChanged( nameof( IsListView ) );
                }
            }
        }
        public bool IsListView => !_isGridView;

        // スライダーの値を保持し、サムネイルサイズを制御するためのプロパティ（サムネイルの横幅）
        private double _thumbnailSize = 260.0;
        public double ThumbnailSize {
            get => _thumbnailSize;
            set
            {
                if ( _thumbnailSize != value ) {
                    _thumbnailSize = value;
                    OnPropertyChanged( nameof( ThumbnailSize ) );
                    OnPropertyChanged( nameof( ThumbnailHeight ) );
                }
            }
        }
        public double ThumbnailHeight => ThumbnailSize * 9.0 / 16.0;    // サムネイルの高さ

        // 動画のソート方法     【暫定】ファイルを更新日時降順にソート
        public VideoService.VideoSortType SortType = VideoService.VideoSortType.LastModifiedDescending;

        private readonly ThumbnailService _thumbnailService;
        private readonly DatabaseService _databaseService;
        private readonly VideoService _videoService;
        private readonly TagService _tagService;

        // コンストラクタ
        public MainViewModel( ) {
            _thumbnailService = new ThumbnailService();

            var dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VideoManager3", "videos.db");
            Directory.CreateDirectory( Path.GetDirectoryName( dbPath )! );
            _databaseService = new DatabaseService( dbPath );
            _tagService = new TagService( _databaseService );
            _videoService = new VideoService( _databaseService, _tagService, new ThumbnailService() );

            // コマンドの初期化
            AddFolderCommand = new CommunityToolkit.Mvvm.Input.RelayCommand( async () =>
            {
                await _videoService.AddVideosFromFolderAsync();
                FilterVideos();
            } );
            AddFilesCommand = new RelayCommand<IEnumerable<string>>(async (files) =>
            {
                if (files != null)
                {
                    await _videoService.AddVideosFromPathsAsync(files);
                    _videoService.SortVideos( SortType );
                    FilterVideos();
                }
            });
            ToggleViewCommand = new CommunityToolkit.Mvvm.Input.RelayCommand( ToggleView );
            EditTagCommand = new CommunityToolkit.Mvvm.Input.RelayCommand( async () => await EditTagAsync(), () => SelectedTag != null );
            UpdateVideoTagsCommand = new RelayCommand<TagItem>( async ( tag ) => await UpdateVideoTagSelection( tag ), ( tag ) => SelectedItem != null );
            DoubleTappedCommand = new CommunityToolkit.Mvvm.Input.RelayCommand( () => _videoService.OpenFile( SelectedItem ), () => SelectedItem != null );

            // 動画とタグの初期読み込み
            _ = LoadInitialDataAsync();
        }

        // 初期データのロード
        public async Task LoadInitialDataAsync() {
            // タグと動画の初期データをロード
            await _videoService.LoadVideosAsync();    // 動画の読み込みを非同期で開始
            await _tagService.LoadTagsAsync();    // タグの読み込みを非同期で開始
            await _videoService.LoadVideoTagsAsync(); // 動画のタグ情報を非同期で読み込み
            await _tagService.LoadTagVideos(_videoService); // タグに動画を関連付ける

            // 【暫定】ファイルを更新日時降順にソート
            _videoService.SortVideos( SortType );
            FilterVideos();
        }

        // タグのフィルタリングを行い、FilteredVideosに結果を格納する
        private void FilterVideos() {
            FilteredVideos.Clear();
            if ( SelectedTag == null || SelectedTag.Name.Equals( "全てのファイル" ) ) {
                foreach ( var video in Videos ) {
                    FilteredVideos.Add( video );
                }
            } else {
                // 「タグなし」だった場合は、タグが紐づいていない動画を全て表示
                if ( SelectedTag.Name.Equals("タグなし") ) {
                    var filtereds = Videos.Where(v => !v.VideoTagItems.Any());
                    foreach ( var video in filtereds )
                        FilteredVideos.Add( video );
                    return;
                }
                var tagIds = new HashSet<int>(SelectedTag.GetAllDescendantIds());
                var filtered = Videos.Where(v => v.VideoTagItems.Any(t => tagIds.Contains(t.Id)));
                foreach ( var video in filtered )
                    FilteredVideos.Add( video );
            }
        }

        // ファイルに対するタグ設定ボタン
        // タグのチェック状態が変更されたときに呼び出されるメソッド
        private async Task UpdateVideoTagSelection( TagItem? tag ) {
            if ( SelectedItem == null || tag == null )
                return;

            // チェック状態に応じてDBとViewModelを更新
            if ( tag.IsChecked ) {
                // タグが既に追加されていなければ追加
                if ( !SelectedItem.VideoTagItems.Any( t => t.Id == tag.Id ) ) {
                    await _databaseService.AddTagToVideoAsync( SelectedItem, tag );
                    SelectedItem.VideoTagItems.Add( tag );
                }
            } else {
                // タグが既に存在すれば削除
                var tagToRemove = SelectedItem.VideoTagItems.FirstOrDefault(t => t.Id == tag.Id);
                if ( tagToRemove != null ) {
                    await _databaseService.RemoveTagFromVideoAsync( SelectedItem, tagToRemove );
                    SelectedItem.VideoTagItems.Remove( tagToRemove );
                }
            }
            await _tagService.LoadTagVideos( _videoService ); // タグに動画を関連付ける
            FilterVideos();
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
        public async Task EditTagAsync() {
            if ( SelectedTag == null )
                return;
            if ( App.MainWindow == null )
                return;

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
            if ( result == ContentDialogResult.Primary
                && !string.IsNullOrWhiteSpace( inputTextBox.Text )
                && SelectedTag.Name != inputTextBox.Text ) {
                SelectedTag.Name = inputTextBox.Text; // ViewModelのプロパティを更新
                // タグツリーを再読み込みしてUIを更新
                await _tagService.AddOrUpdateTagAsync( SelectedTag );
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
    }
}