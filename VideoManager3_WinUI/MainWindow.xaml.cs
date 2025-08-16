using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Windows.ApplicationModel.DataTransfer;
using Windows.Devices.Display.Core;
using Windows.Storage;

namespace VideoManager3_WinUI {
    public sealed partial class MainWindow:Window {
        public MainViewModel ViewModel { get; }

        public MainWindow() {
            this.InitializeComponent();
            this.Title = "動画管理くん";

            ViewModel = new MainViewModel();
            (this.Content as FrameworkElement)!.DataContext = ViewModel;

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

        // ファイル名昇順ソートのイベントハンドラー
        private void SortByNameAscending( object sender, RoutedEventArgs e ) {
            ViewModel.SortType = VideoSortType.FileNameAscending;
        }

        // ファイルをダブルクリックしたときのイベントハンドラー
        private void GridView_DoubleTapped( object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e ) {
            if ( e.OriginalSource is FrameworkElement element && element.DataContext is VideoItem videoItem ) {
                ViewModel.DoubleTappedCommand.Execute( null );
            }
        }

        // タグの右クリック編集を実行するイベントハンドラー（ツリー選択時とファイル選択時で共用できる？）
        private void TagEdit( object sender, RoutedEventArgs e ) {
            ViewModel.EditTagCommand.Execute( null );
        }

        // 動画に対するタグ設定フライアウトが開かれる直前のイベントハンドラ
        private void TagEditFlyout_Opening( object sender, object e ) {
            // ViewModelに、現在選択中の動画に合わせてタグのチェック状態を更新させる
            ViewModel.PrepareTagsForEditing();

            if ( sender is MenuFlyout flyout ) {
                // 既存の項目をクリア
                flyout.Items.Clear();
                // ViewModelのTagItemsからメニューを再帰的に構築
                CreateTagMenuItems( flyout.Items, ViewModel.TagItems );
            }
        }

        // タグ情報から動的にメニュー項目を生成する再帰メソッド
        private void CreateTagMenuItems( IList<MenuFlyoutItemBase> menuItems, IEnumerable<TagItem> tagItems ) {
            foreach ( var tag in tagItems ) {
                if ( tag.IsGroup ) {
                    // タグがグループの場合、サブメニューを作成
                    var subItem = new MenuFlyoutSubItem { Text = tag.Name };
                    CreateTagMenuItems( subItem.Items, tag.Children );
                    menuItems.Add( subItem );
                } else {
                    // タグがグループでない場合、チェックボックス付きのメニュー項目を作成
                    var toggleItem = new ToggleMenuFlyoutItem
                    {
                        Text = tag.Name,
                        IsChecked = tag.IsChecked,
                        // TagプロパティにTagItemインスタンスを格納しておく
                        Tag = tag
                    };
                    // ClickイベントでViewModelのコマンドを呼び出す
                    toggleItem.Click += ToggleTag_Click;
                    menuItems.Add( toggleItem );
                }
            }
        }

        // チェックボックス付きメニュー項目がクリックされたときのイベントハンドラ
        private void ToggleTag_Click( object sender, RoutedEventArgs e ) {
            if ( sender is ToggleMenuFlyoutItem toggleItem && toggleItem.Tag is TagItem tagItem ) {
                // IsCheckedプロパティはクリック後に更新されるため、現在の値を反転させた値をViewModelに伝える
                // ただし、ToggleMenuFlyoutItemがクリックされるとIsCheckedは自動でトグルするので、
                // tagItemのIsCheckedプロパティをUIと同期させてからコマンドを実行する
                tagItem.IsChecked = toggleItem.IsChecked;
                ViewModel.UpdateVideoTagsCommand.Execute( tagItem );
            }
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

        // 検索ボックスのテキストが変更されたときのイベントハンドラ
        private void SearchBox_TextChanged( AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args ) {
            // TextChangedイベントは、ユーザーの入力だけでなく、プログラムによるテキストの変更でも発生します。
            // ユーザーの操作によってのみフィルタリングを実行するために、args.Reasonを確認します。
            if ( args.Reason == AutoSuggestionBoxTextChangeReason.UserInput ) {
                //ViewModel.FilterVideos();
            }
        }

        // 検索ボックスのクエリが送信されたときのイベントハンドラ
        private void SearchBox_QuerySubmitted( AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args ) {
            //ViewModel.FilterVideos();
        }
    }
}