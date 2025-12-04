using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using VideoManager3_WinUI.Models;
using VideoManager3_WinUI.Services;
using VideoManager3_WinUI.ViewModels;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics;
using Windows.Storage;
using WinRT.Interop;

namespace VideoManager3_WinUI {
    public sealed partial class MainWindow:Window {
        public MainViewModel ViewModel { get; }

        private SettingService _settingService;
        private AppWindow _appWindow;

        // コンテキストメニューが開いているかを追跡するフラグ
        private bool _isFileNameContextFlyoutOpen = false;

        public MainWindow() {
            this.InitializeComponent();
            this.Title = "動画管理くん";

            // ViewModelの初期化とデータコンテキストの設定
            ViewModel = new MainViewModel();
            (this.Content as FrameworkElement)!.DataContext = ViewModel;
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
            ViewModel.ScrollToItemRequested += ViewModel_ScrollToItemRequested;

            // 設定サービスの初期化と設定のロード
            _settingService = new SettingService();
            IntPtr hWnd = WindowNative.GetWindowHandle(this);
            WindowId wndId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
            _appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId( wndId );
            LoadSetting();

            // FileNameTextBox 用のカスタムコンテキストメニューを設定
            SetupFileNameContextFlyout();

            // タグツリーでのタップを処理（タグ編集中にラベルをタップしてチェックを切替えるため）
            TagsTreeView.Tapped += TagsTreeView_Tapped;

            // 初期の選択ファイルの文字列を設定
            FileNameTextBox.Text = ViewModel.SelectedItem?.FileNameWithoutArtists ?? string.Empty;
        }

        private void SetupFileNameContextFlyout() {
            // 標準的なコピー/切り取り/貼り付け/全選択メニューを作成し、
            // クリック時に TextBox のメソッドを直接呼び出す
            var mf = new MenuFlyout();

            var copy = new MenuFlyoutItem { Text = "コピー" };
            copy.Click += ( s, e ) => {
                try { FileNameTextBox.CopySelectionToClipboard(); } catch { }
            };
            mf.Items.Add( copy );

            var cut = new MenuFlyoutItem { Text = "切り取り" };
            cut.Click += ( s, e ) => {
                try { FileNameTextBox.CutSelectionToClipboard(); } catch { }
            };
            mf.Items.Add( cut );

            var paste = new MenuFlyoutItem { Text = "貼り付け" };
            paste.Click += ( s, e ) => {
                try { FileNameTextBox.PasteFromClipboard(); } catch { }
            };
            mf.Items.Add( paste );

            var selectAll = new MenuFlyoutItem { Text = "全て選択" };
            selectAll.Click += ( s, e ) => {
                try { FileNameTextBox.SelectAll(); } catch { }
            };
            mf.Items.Add( selectAll );

            // 開閉イベントでフラグを管理し、閉じたら編集状態にフォーカス戻す
            mf.Opened += ( s, e ) => { _isFileNameContextFlyoutOpen = true; };
            mf.Closed += ( s, e ) => {
                _isFileNameContextFlyoutOpen = false;
                // メニュー操作後はテキスト編集を継続できるようフォーカスを戻す
                FileNameTextBox.Focus( FocusState.Programmatic );
            };

            FileNameTextBox.ContextFlyout = mf;
        }

        /// <summary>
        /// タグツリー上でタップされたとき、タグ編集中であればそのタグのチェックを切り替える
        /// </summary>
        private void TagsTreeView_Tapped( object sender, TappedRoutedEventArgs e ) {
            // タグ編集モードでなければ何もしない（既存の挙動を維持）
            if ( !ViewModel.TagTreeViewModel.IsTagSetting ) {
                return;
            }

            var original = e.OriginalSource as DependencyObject;
            if ( original == null )
                return;

            // チェックボックス部分をタップした場合は既存の CheckBox の挙動に任せる（二重トグル防止）
            if ( IsChildOf<CheckBox>( original, null ) ) {
                return;
            }

            // クリックされた要素から DataContext が TagItem の親を探索する
            var tag = FindDataContext<TagItem>(original);
            if ( tag != null ) {
                // チェックボックスのトグル（チェック状態は XAML のバインディングで反映される）
                tag.IsChecked = !tag.IsChecked;
                e.Handled = true;
            }
        }

        // 指定の起点から親方向に辿って DataContext が T の FrameworkElement を見つけるユーティリティ
        private T? FindDataContext<T>( DependencyObject? start ) where T : class {
            while ( start != null ) {
                if ( start is FrameworkElement fe && fe.DataContext is T t ) {
                    return t;
                }
                start = VisualTreeHelper.GetParent( start );
            }
            return null;
        }

        /// <summary>
        /// 選択されているアイテムまでスクロールする
        /// </summary>
        private void ViewModel_ScrollToItemRequested( VideoItem item ) {
            if ( item == null )
                return;

            DispatcherQueue.TryEnqueue( () => {
                if ( ViewModel.UIManager.IsListView ) {
                    VideoListView.ScrollIntoView( item, ScrollIntoViewAlignment.Default );
                } else {
                    VideoGridView.ScrollIntoView( item, ScrollIntoViewAlignment.Default );
                }
            } );
        }

        // GridViewのUI仮想化と連携してサムネイルを遅延読み込みする
        private void GridView_ContainerContentChanging( ListViewBase sender, ContainerContentChangingEventArgs args ) {
            if ( args.InRecycleQueue ) {
                if ( args.Item is VideoItem itemToUnload ) {
                    // メモリを解放するためにBitmapImageをクリア
                    itemToUnload.UnloadThumbnailImage();
                }
                return;
            }

            if ( args.Item is VideoItem itemToLoad ) {
                // まだBitmapImageが読み込まれていない、かつ元データが存在する場合のみ、非同期読み込みを開始
                if ( itemToLoad.ThumbnailImage == null && itemToLoad.Thumbnail != null ) {
                    // RegisterUpdateCallbackはUIスレッドでコールバックを実行する
                    args.RegisterUpdateCallback( async ( s, e ) => {
                        // UIスレッドで非同期に画像を読み込んで設定する
                        await itemToLoad.LoadThumbnailImageAsync();
                    } );
                }
            }
        }

        // アプリを閉じるときのイベントハンドラー
        private async void Window_Closed( object sender, WindowEventArgs args ) {
            SaveSettings();
            await ViewModel.WindowCloseAsync();
        }

        // いいねボタンを左クリックしたときのイベントハンドラー
        private void LikeButton_Click( object sender, RoutedEventArgs e ) {
            if ( ViewModel.SelectedItem != null ) {
                ViewModel.SelectedItem.LikeCount++;
                foreach ( var artist in ViewModel.SelectedItem.ArtistsInVideo ) {
                    artist.LikeCount++;
                }
            }
        }

        // いいねボタンを右クリックしたときのイベントハンドラー
        private void LikeButton_RightTapped( object sender, RightTappedRoutedEventArgs e ) {
            if ( ViewModel.SelectedItem != null ) {
                if ( ViewModel.SelectedItem.LikeCount > 0 ) {
                    ViewModel.SelectedItem.LikeCount--;
                    foreach ( var artist in ViewModel.SelectedItem.ArtistsInVideo ) {
                        artist.LikeCount--;
                    }
                }
            }
        }

        // 左ペインの表示を切り替える（タグツリービュー ←→ アーティスト一覧）
        private void ToggleLeftPain_Click( object sender, RoutedEventArgs e ) {
            ViewModel.UIManager.IsTreeView = !ViewModel.UIManager.IsTreeView;
        }

        // ファイルをダブルクリックしたときのイベントハンドラー
        private void GridView_DoubleTapped( object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e ) {
            if ( e.OriginalSource is FrameworkElement element && element.DataContext is VideoItem videoItem ) {
                ViewModel.DoubleTappedCommand.Execute( videoItem );
            }
        }

        // ランダム再生ボタンをクリックしたときのイベントハンドラー
        private void RandomButton_Click( object sender, RoutedEventArgs e ) {
            if ( ViewModel.FilteredVideos.Count > 0 ) {
                var random = new Random().Next( ViewModel.FilteredVideos.Count );
                var videoItem = ViewModel.FilteredVideos[random];
                ViewModel.DoubleTappedCommand.Execute( videoItem );

                ViewModel.SelectedItem = videoItem;
            }
        }

        // ソートボタンをクリックしたときのイベントハンドラー
        private void SortButton_Click( object sender, RoutedEventArgs e ) {
            var flyout = new MenuFlyout();

            var sortByNameAsc = new ToggleMenuFlyoutItem { Text = "ファイル名 (昇順)", IsChecked = ViewModel.SortType == VideoSortType.FileNameAscending };
            sortByNameAsc.Click += ( s, e ) => ViewModel.SortType = VideoSortType.FileNameAscending;
            flyout.Items.Add( sortByNameAsc );

            var sortByNameDesc = new ToggleMenuFlyoutItem { Text = "ファイル名 (降順)", IsChecked = ViewModel.SortType == VideoSortType.FileNameDescending };
            sortByNameDesc.Click += ( s, e ) => ViewModel.SortType = VideoSortType.FileNameDescending;
            flyout.Items.Add( sortByNameDesc );

            var sortByDateAsc = new ToggleMenuFlyoutItem { Text = "更新日時 (古い順)", IsChecked = ViewModel.SortType == VideoSortType.LastModifiedAscending };
            sortByDateAsc.Click += ( s, e ) => ViewModel.SortType = VideoSortType.LastModifiedAscending;
            flyout.Items.Add( sortByDateAsc );

            var sortByDateDesc = new ToggleMenuFlyoutItem { Text = "更新日時 (新しい順)", IsChecked = ViewModel.SortType == VideoSortType.LastModifiedDescending };
            sortByDateDesc.Click += ( s, e ) => ViewModel.SortType = VideoSortType.LastModifiedDescending;
            flyout.Items.Add( sortByDateDesc );

            var sortBySizeAsc = new ToggleMenuFlyoutItem { Text = "ファイルサイズ (小さい順)", IsChecked = ViewModel.SortType == VideoSortType.FileSizeAscending };
            sortBySizeAsc.Click += ( s, e ) => ViewModel.SortType = VideoSortType.FileSizeAscending;
            flyout.Items.Add( sortBySizeAsc );

            var sortBySizeDesc = new ToggleMenuFlyoutItem { Text = "ファイルサイズ (大きい順)", IsChecked = ViewModel.SortType == VideoSortType.FileSizeDescending };
            sortBySizeDesc.Click += ( s, e ) => ViewModel.SortType = VideoSortType.FileSizeDescending;
            flyout.Items.Add( sortBySizeDesc );

            var sortByLikesAsc = new ToggleMenuFlyoutItem { Text = "いいね数 (少ない順)", IsChecked = ViewModel.SortType == VideoSortType.LikeCountAscending };
            sortByLikesAsc.Click += ( s, e ) => ViewModel.SortType = VideoSortType.LikeCountAscending;
            flyout.Items.Add( sortByLikesAsc );

            var sortByLikesDesc = new ToggleMenuFlyoutItem { Text = "いいね数 (多い順)", IsChecked = ViewModel.SortType == VideoSortType.LikeCountDescending };
            sortByLikesDesc.Click += ( s, e ) => ViewModel.SortType = VideoSortType.LikeCountDescending;
            flyout.Items.Add( sortByLikesDesc );

            flyout.ShowAt( sender as FrameworkElement );
        }

        // タグの右クリック編集を実行するイベントハンドラー
        private void TagEdit( object sender, RoutedEventArgs e ) {
            // sender（クリックされたMenuFlyoutItem）のDataContextを取得
            if ( sender is FrameworkElement element && element.DataContext is TagItem selectedTag ) {
                ViewModel.TagTreeViewModel.EditTagCommand.Execute( selectedTag );
            }
        }

        // タグ設定モード開始時の動画アイテム
        private VideoItem? _editTargetItem;

        // 動画に対するタグ設定イベントハンドラ
        private async void TagEditFlyout_Opening( object sender, object e ) {

            if ( ViewModel.TagTreeViewModel.IsTagSetting == false ) {
                // 現在選択中の動画に合わせてタグのチェック状態を設定
                ViewModel.TagTreeViewModel.PrepareTagsForEditing( ViewModel.SelectedItem );
                ViewModel.TagTreeViewModel.IsTagSetting = true;
                _editTargetItem = ViewModel.SelectedItem;
            } else {
                // 変更を確定させDBに保存
                await ViewModel.TagTreeViewModel.UpdateVideoTagSelection( _editTargetItem );
                ViewModel.TagTreeViewModel.IsTagSetting = false;
            }
        }

        // タグ設定モード中に画面の他の部分をタップしたときのイベントハンドラ
        private async void RootGrid_Tapped( object sender, TappedRoutedEventArgs e ) {
            if ( !ViewModel.TagTreeViewModel.IsTagSetting ) {
                return;
            }

            var originalSource = e.OriginalSource as DependencyObject;

            // TagsTreeViewまたはその子の場合は何もしない
            if ( IsChildOf<TreeView>( originalSource, "TagsTreeView" ) ) {
                return;
            }

            // TagEditButtonまたはその子の場合は何もしない
            if ( IsChildOf<AppBarButton>( originalSource, "TagEditButton" ) ) {
                return;
            }

            // それ以外の場合は変更を確定
            await ViewModel.TagTreeViewModel.UpdateVideoTagSelection( _editTargetItem );
            ViewModel.TagTreeViewModel.IsTagSetting = false;
        }

        // 指定した型および名前の親要素が存在するかどうかを確認するヘルパーメソッド
        private bool IsChildOf<T>( DependencyObject? element, string? name ) where T : FrameworkElement {
            while ( element != null ) {
                if ( element is T targetElement ) {
                    if ( name == null || targetElement.Name == name ) {
                        return true;
                    }
                }
                element = VisualTreeHelper.GetParent( element );
            }
            return false;
        }

        // アーティストのお気に入りアイコンをクリックしたときのイベントハンドラ
        // アーティスト一覧ペインと、動画詳細ペインの両方で使用
        private void FavoriteIcon_PointerPressed( object sender, PointerRoutedEventArgs e ) {
            if ( sender is FrameworkElement element && element.DataContext is ArtistItem artist ) {
                artist.IsFavorite = !artist.IsFavorite;
                e.Handled = true;
            }
        }

        // アーティスト名をクリックしたときのイベントハンドラ 選択状態にする
        // 動画詳細ペインで使用
        private void ArtistName_PointerPressed( object sender, PointerRoutedEventArgs e ) {
            if ( sender is FrameworkElement element && element.DataContext is ArtistItem artist ) {
                ViewModel.SelectedArtist = artist;
                e.Handled = true;
            }
        }

        // ドラッグ中のアイテムがウィンドウ上にあるときのイベントハンドラ
        // ホームフォルダの末尾20文字をキャプションとして表示する
        private void Window_DragOver( object sender, DragEventArgs e ) {
            e.AcceptedOperation = DataPackageOperation.Copy;

            if ( e.DragUIOverride != null ) {
                int pathLength = ViewModel.HomeFolderPath.Length;
                int maxLength = 20;
                string displayPath = "ファイルを追加";
                if ( pathLength > maxLength ) {
                    displayPath = ViewModel.HomeFolderPath.Substring( pathLength - maxLength );
                } else {
                    displayPath = ViewModel.HomeFolderPath;
                }
                e.DragUIOverride.Caption = displayPath;
                e.DragUIOverride.IsContentVisible = true;
            }
        }

        /// <summary>
        /// アプリ画面上にドラッグ＆ドロップでファイルがドロップされたときのイベントハンドラ
        /// JSON形式の一時ファイルを作成し、FileMoverを実行
        /// </summary>
        private async void Window_Drop( object sender, DragEventArgs e ) {
            if ( e.DataView.Contains( StandardDataFormats.StorageItems ) ) {
                var storageItems = await e.DataView.GetStorageItemsAsync();
                var sourceFilePaths = storageItems.OfType<StorageFile>().Select( f => f.Path ).ToList();

                if ( !sourceFilePaths.Any() ) {
                    return;
                }

                // 1. 移動情報を定義
                var moveTask = new
                {
                    SourceFiles = sourceFilePaths,
                    DestinationFolder = ViewModel.HomeFolderPath
                };

                // 2. 情報を一時JSONファイルにシリアライズ
                string tempFilePath = Path.GetTempFileName() + ".json";
                await File.WriteAllTextAsync( tempFilePath, System.Text.Json.JsonSerializer.Serialize( moveTask ) );

                // 3. ヘルパーアプリを起動し、JSONファイルのパスを渡す
                string helperAppPath = @"<パス>\VideoManager3.FileMover.exe"; // 本来はもっと動的に取得すべき
                Process.Start( helperAppPath, $"\"{tempFilePath}\"" );
            }
        }

        // 画面などの初期状態をロード
        public void LoadSetting() {
            var setting = _settingService.LoadSettings();
            if ( setting != null ) {
                ViewModel.SortType = (VideoSortType)setting.VideoSortType;
                ViewModel.UIManager.IsGridView = setting.IsGridView;
                ViewModel.UIManager.ThumbnailSize = setting.ThumbnailSize;
                ViewModel.HomeFolderPath = setting.HomeFolderPath;

                // ウィンドウが画面外に出てしまうのを防ぐ
                var requestRect = new RectInt32((int)setting.WindowLeft, (int)setting.WindowTop, (int)setting.WindowWidth, (int)setting.WindowHeight);
                var displayArea = DisplayArea.GetFromRect(requestRect, DisplayAreaFallback.Primary);
                var workArea = displayArea.WorkArea;

                var windowLeft = requestRect.X;
                var windowTop = requestRect.Y;
                var windowWidth = requestRect.Width;
                var windowHeight = requestRect.Height;

                // ウィンドウが完全に作業領域内に収まるように位置を調整
                if ( windowLeft < workArea.X )
                    windowLeft = workArea.X;
                if ( windowTop < workArea.Y )
                    windowTop = workArea.Y;
                if ( windowLeft + windowWidth > workArea.X + workArea.Width )
                    windowLeft = workArea.X + workArea.Width - windowWidth;
                if ( windowTop + windowHeight > workArea.Y + workArea.Height )
                    windowTop = workArea.Y + workArea.Height - windowHeight;

                _appWindow.MoveAndResize( new RectInt32( windowLeft, windowTop, windowWidth, windowHeight ) );

                if ( setting.IsFullScreen && _appWindow.Presenter is OverlappedPresenter overlappedPresenter ) {
                    overlappedPresenter.Maximize();
                }
            }
        }
        // 設定を保存する
        private void SaveSettings() {
            var settings = _settingService.LoadSettings() ?? new SettingItem();

            settings.VideoSortType = (int)ViewModel.SortType;
            settings.IsGridView = ViewModel.UIManager.IsGridView;
            settings.ThumbnailSize = ViewModel.UIManager.ThumbnailSize;
            settings.HomeFolderPath = ViewModel.HomeFolderPath;

            if ( _appWindow.Presenter is OverlappedPresenter presenter ) {
                settings.IsFullScreen = presenter.State == OverlappedPresenterState.Maximized;
                if ( presenter.State == OverlappedPresenterState.Restored ) {
                    settings.WindowLeft = _appWindow.Position.X;
                    settings.WindowTop = _appWindow.Position.Y;
                    settings.WindowWidth = _appWindow.Size.Width;
                    settings.WindowHeight = _appWindow.Size.Height;
                }
            }
            _settingService.SaveSettings( settings );
        }

        // ファイル名テキストボックスのイベントハンドラー
        // テキストボックスがフォーカスを得たときに元のファイル名を表示する
        private void FileNameTextBox_GotFocus( object sender, RoutedEventArgs e ) {
            if ( ViewModel.SelectedItem is VideoItem selectedItem && sender is TextBox textBox ) {
                textBox.Text = selectedItem.FileName;
            }
        }

        // ファイル名テキストボックスのイベントハンドラー
        // テキストボックスがフォーカスを失ったときに変更を確定する
        private async void FileNameTextBox_LostFocus( object sender, RoutedEventArgs e ) {
            if ( !(sender is TextBox textBox) || !(ViewModel.SelectedItem is VideoItem selectedItem) ) {
                return;
            }

            // メニューが開いている間はコミットしない（メニュー操作でコピー/切り取りを行うため）
            await Task.Delay( 50 ); // 多少の遅延で安定させる
            if ( _isFileNameContextFlyoutOpen ) {
                // メニューが閉じた後にフォーカスを戻すため、Closed ハンドラ側で対応しているのでここでは何もしない
                return;
            }

            if ( selectedItem.FileName != textBox.Text ) {
                await ViewModel.RenameFileAsync( selectedItem, textBox.Text );
            }
            textBox.Text = selectedItem.FileNameWithoutArtists;
        }

        // ファイル名編集テキストボックスのキーイベントハンドラー
        private void FileNameTextBox_KeyDown( object sender, KeyRoutedEventArgs e ) {
            if ( e.Key == Windows.System.VirtualKey.Enter ) {
                // Enterキーでフォーカスを外して編集を確定させる
                FocusSelectedItem();
            } else if ( e.Key == Windows.System.VirtualKey.Escape ) {
                // Escapeキーで編集をキャンセルして元のファイル名に戻す
                if ( sender is TextBox textBox && ViewModel.SelectedItem is VideoItem selectedItem ) {
                    textBox.Text = selectedItem.FileName;
                }
                // フォーカスを選択中アイテムに戻す
                FocusSelectedItem();
            }
        }

        // ListView / GridView でキーが押されたときのイベントハンドラ
        private void FocusSelectedItem() {
            DispatcherQueue.TryEnqueue( () => {
                if ( ViewModel.UIManager.IsGridView ) {
                    VideoGridView.Focus( FocusState.Programmatic );
                    if ( ViewModel.SelectedItem != null ) {
                        VideoGridView.ScrollIntoView( ViewModel.SelectedItem );
                    }
                } else {
                    VideoListView.Focus( FocusState.Programmatic );
                    if ( ViewModel.SelectedItem != null ) {
                        VideoListView.ScrollIntoView( ViewModel.SelectedItem );
                    }
                }
            } );
        }

        private void VideoList_KeyDown( object sender, KeyRoutedEventArgs e ) {
            if ( e.Key == Windows.System.VirtualKey.Delete ) {
                if ( ViewModel.DeleteFileCommand.CanExecute( null ) ) {
                    ViewModel.DeleteFileCommand.Execute( null );
                }
            }
            if ( e.Key == Windows.System.VirtualKey.F2 )
            {
                if (ViewModel.SelectedItem != null)
                {
                    FileNameTextBox.Focus(FocusState.Programmatic);
                    FileNameTextBox.SelectAll();
                }
            }
        }

        /// <summary>
        /// ファイル名編集中に選択アイテムが変更されたときに、変更を確定させる
        /// </summary>
        private void ViewModel_PropertyChanged( object? sender, System.ComponentModel.PropertyChangedEventArgs e ) {
            if ( e.PropertyName == nameof( ViewModel.SelectedItem ) ) {
                if ( FileNameTextBox.FocusState == FocusState.Unfocused ) {
                    FileNameTextBox.Text = ViewModel.SelectedItem?.FileNameWithoutArtists ?? string.Empty;
                }
            }
        }

        // ファイルの保存場所をエクスプローラーで開く
        private void OpenFileLocation_Click( object sender, RoutedEventArgs e ) {
            if ( ViewModel.SelectedItem != null ) {
                string? path = ViewModel.SelectedItem.FilePath;
                if ( !string.IsNullOrEmpty( path ) && File.Exists( path ) ) {
                    Process.Start( "explorer.exe", $"/select, \"{path}\"" );
                }
            }
        }

        private void VideoGrid_PointerPressed( object sender, PointerRoutedEventArgs e ) {
            var pointerPoint = e.GetCurrentPoint(sender as UIElement);
            if ( pointerPoint.Properties.IsRightButtonPressed ) {
                if ( sender is FrameworkElement element && element.DataContext is VideoItem videoItem ) {
                    ViewModel.SelectedItem = videoItem;
                }
            }
        }
    }
}
