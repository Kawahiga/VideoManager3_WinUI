using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics;
using Windows.Storage;
using WinRT.Interop;

namespace VideoManager3_WinUI {
    public sealed partial class MainWindow:Window {
        public MainViewModel ViewModel { get; }

        private SettingService _settingService;
        private AppWindow _appWindow;

        public MainWindow() {
            this.InitializeComponent();
            this.Title = "動画管理くん";

            ViewModel = new MainViewModel();
            (this.Content as FrameworkElement)!.DataContext = ViewModel;

            _settingService = new SettingService();
            IntPtr hWnd = WindowNative.GetWindowHandle(this);
            WindowId wndId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
            _appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId( wndId );
            LoadSetting();
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
            }
        }

        // いいねボタンを右クリックしたときのイベントハンドラー
        private void LikeButton_RightTapped( object sender, RightTappedRoutedEventArgs e ) {
            if ( ViewModel.SelectedItem != null ) {
                if ( ViewModel.SelectedItem.LikeCount > 0 ) {
                    ViewModel.SelectedItem.LikeCount--;
                }
            }
        }

        // 左ペインの表示を切り替える（タグツリービュー ←→ アーティスト一覧）
        private void ToggleLeftPain_Click( object sender, RoutedEventArgs e ) {
            ViewModel.IsTreeView = !ViewModel.IsTreeView;
        }

        // ファイルをダブルクリックしたときのイベントハンドラー
        private void GridView_DoubleTapped( object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e ) {
            if ( e.OriginalSource is FrameworkElement element && element.DataContext is VideoItem videoItem ) {
                ViewModel.DoubleTappedCommand.Execute( null );
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
                // DataContextがTagItemであれば、それをコマンドのパラメータとして渡す
                ViewModel.EditTagCommand.Execute( selectedTag );
            }
        }

        // タグ設定モード開始時の動画アイテム
        private VideoItem? _editTargetItem;

        // 動画に対するタグ設定イベントハンドラ
        private void TagEditFlyout_Opening( object sender, object e ) {

            if ( ViewModel.IsTagSetting == false ) {
                // 現在選択中の動画に合わせてタグのチェック状態を設定
                ViewModel.PrepareTagsForEditing();
                ViewModel.IsTagSetting = true;
                _editTargetItem = ViewModel.SelectedItem;
            } else {
                // 変更を確定させDBに保存
                ViewModel.UpdateVideoTagsCommand.Execute( _editTargetItem );
                ViewModel.IsTagSetting = false;
            }
        }

        // タグ設定モード中に画面の他の部分をタップしたときのイベントハンドラ
        private void RootGrid_Tapped( object sender, TappedRoutedEventArgs e ) {
            if ( !ViewModel.IsTagSetting ) {
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
            ViewModel.UpdateVideoTagsCommand.Execute( _editTargetItem );
            ViewModel.IsTagSetting = false;
        }

        private bool IsChildOf<T>( DependencyObject element, string name = null ) where T : FrameworkElement {
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

        // ドラッグ中のアイテムがウィンドウ上にあるときのイベントハンドラ
        // ホームフォルダの末尾20文字をキャプションとして表示する
        private void Window_DragOver( object sender, DragEventArgs e ) {
            e.AcceptedOperation = DataPackageOperation.Copy;

            if ( e.DragUIOverride != null ) {
                int pathLength = ViewModel.HomeFolderPath.Length;
                int maxLength = 20;
                string displayPath= "ファイルを追加";
                if ( pathLength > maxLength ) {
                    displayPath = ViewModel.HomeFolderPath.Substring( pathLength - maxLength );
                } else {
                    displayPath = ViewModel.HomeFolderPath;
                }
                e.DragUIOverride.Caption = displayPath;
                e.DragUIOverride.IsContentVisible = true;
            }
        }

        // ドラッグ＆ドロップでファイルがドロップされたときのイベントハンドラ
        // 未実装項目
        // ・既にファイルが存在する場合の処理
        // ・元ファイルの削除（現状コピーで実装）なぜか読み取り専用になる
        private async void Window_Drop( object sender, DragEventArgs e ) {
            if ( e.DataView.Contains( StandardDataFormats.StorageItems ) ) {
                var storageItems = await e.DataView.GetStorageItemsAsync();
                if ( storageItems.Any() ) {
                    var droppedFiles = storageItems.OfType<StorageFile>().ToList();
                    if ( droppedFiles.Any() ) {
                        // ViewModel.HomeFolderPath を StorageFolder オブジェクトに変換
                        StorageFolder? targetFolder = null;
                        try {
                            targetFolder = await StorageFolder.GetFolderFromPathAsync( ViewModel.HomeFolderPath );
                        } catch ( Exception ex ) {
                            // エラーハンドリング: ViewModel.HomeFolderPath が無効なパスの場合など
                            System.Diagnostics.Debug.WriteLine( $"Error getting target folder: {ex.Message}" );
                            return; // 処理を中断
                        }

                        List<string> newFilePaths = new List<string>();
                        foreach ( var file in droppedFiles ) {
                            try {
                                // ファイルをコピーし、コピーされたファイルのStorageFileオブジェクトを取得
                                StorageFile copiedFile = await file.CopyAsync(targetFolder, file.Name, NameCollisionOption.GenerateUniqueName);
                                newFilePaths.Add( copiedFile.Path );

                                //// 元のファイルを一時フォルダに移動して後で削除
                                //StorageFolder tempFolder = ApplicationData.Current.TemporaryFolder;
                                //// ファイル名が重複しないようにユニークな名前を生成
                                //string tempFileName = $"{Guid.NewGuid()}_{file.Name}";
                                //await file.MoveAsync( tempFolder, tempFileName, NameCollisionOption.ReplaceExisting ); // ReplaceExisting if a GUID collision occurs (highly unlikely)

                            } catch ( Exception ex ) {
                                // エラーハンドリング: ファイル操作に失敗した場合
                                System.Diagnostics.Debug.WriteLine( $"Error processing file {file.Name}: {ex.Message}" );
                            }
                        }

                        if ( newFilePaths.Any() ) {
                            // ViewModelに移動後のファイルパスを追加する処理を依頼する
                            ViewModel.AddFilesCommand.Execute( newFilePaths );
                        }
                    }
                }
            }
        }

        // 画面などの初期状態をロード
        public void LoadSetting() {
            var setting = _settingService.LoadSettings();
            if ( setting != null ) {
                ViewModel.SortType = (VideoSortType)setting.VideoSortType;
                ViewModel.IsGridView = setting.IsGridView;
                ViewModel.ThumbnailSize = setting.ThumbnailSize;
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
            settings.IsGridView = ViewModel.IsGridView;
            settings.ThumbnailSize = ViewModel.ThumbnailSize;
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
    }
}