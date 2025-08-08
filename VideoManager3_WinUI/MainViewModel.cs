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
        //public ObservableCollection<VideoItem> Videos { get; } = new ObservableCollection<VideoItem>();
        public ObservableCollection<VideoItem> Videos => _videoService.Videos;

        public ObservableCollection<TagItem> TagItems => _tagService.TagItems;

        public ICommand AddFolderCommand { get; }   // フォルダを指定してファイルを読み込むコマンド
        public ICommand ToggleViewCommand { get; }  // ビュー切り替えコマンド（グリッドビューとリストビューの切り替え）
        public ICommand EditTagCommand { get; }     // タグ編集コマンド
        public ICommand UpdateVideoTagsCommand { get; } // 動画のタグ情報を更新するコマンド
        public ICommand DoubleTappedCommand { get; }    // ファイルをダブルクリックしたときのコマンド

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
                    ((RelayCommand)DoubleTappedCommand).RaiseCanExecuteChanged();
                    ((RelayCommand)UpdateVideoTagsCommand).RaiseCanExecuteChanged();
                }
            }
        }

        // TreeViewで選択されているタグを保持するためのプロパティ
        private TagItem? _selectedTag;
        public TagItem? SelectedTag {
            get => _selectedTag;
            set
            {
                _selectedTag = value;
                OnPropertyChanged( nameof( SelectedTag ) );
                // 選択状態が変わったら、コマンドの実行可能性を再評価するよう通知
                ((RelayCommand)EditTagCommand).RaiseCanExecuteChanged();
            }
        }

        private readonly DispatcherQueue _dispatcherQueue;
        private readonly ThumbnailService _thumbnailService;
        private readonly DatabaseService _databaseService;
        private readonly VideoService _videoService;
        private readonly TagService _tagService;
        
        // コンストラクタ
        public MainViewModel( DispatcherQueue dispatcherQueue ) {
            _dispatcherQueue = dispatcherQueue;
            _thumbnailService = new ThumbnailService();

            var dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VideoManager3", "videos.db");
            Directory.CreateDirectory( Path.GetDirectoryName( dbPath )! );
            _databaseService = new DatabaseService( dbPath );
            _tagService = new TagService( _databaseService );
            _videoService = new VideoService( _databaseService, _tagService, new ThumbnailService(), dispatcherQueue );

            // コマンドの初期化
            AddFolderCommand = new RelayCommand( async () => await _videoService.AddVideosFromFolderAsync() );
            ToggleViewCommand = new RelayCommand( ToggleView );
            EditTagCommand = new RelayCommand( async () => await EditTagAsync(), () => SelectedTag != null );
            UpdateVideoTagsCommand = new RelayCommand<TagItem>( async ( tag ) => await UpdateVideoTagSelection( tag ), ( tag ) => SelectedItem != null );
            DoubleTappedCommand = new RelayCommand( () => _videoService.OpenFile( SelectedItem ), () => SelectedItem != null );

            // 動画とタグの初期読み込み
            _ = LoadInitialDataAsync();
        }

        // 初期データのロード
        public async Task LoadInitialDataAsync() {
            // タグと動画の初期データをロード
            await _tagService.LoadTagsAsync();    // タグの読み込みを非同期で開始
            await _videoService.LoadVideosAsync();    // 動画の読み込みを非同期で開始

            // 【暫定】ファイルを更新日時降順にソート
            _videoService.SortVideosByLastModified( true );
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
            if ( SelectedTag == null )return;
            if ( App.MainWindow == null ) return;

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
                //await _databaseService.AddOrUpdateTagAsync( SelectedTag ); // データベースを更新
                //await LoadTagsAsync(); // タグツリーを再読み込みしてUIを更新
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
