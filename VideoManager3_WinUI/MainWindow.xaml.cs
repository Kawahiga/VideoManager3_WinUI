using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace VideoManager3_WinUI {
    public sealed partial class MainWindow:Window {
        public MainViewModel ViewModel { get; }

        public MainWindow() {
            this.InitializeComponent();
            this.Title = "動画管理くん";

            ViewModel = new MainViewModel( );
            (this.Content as FrameworkElement)!.DataContext = ViewModel;

        }

        // GridViewのUI仮想化と連携してサムネイルを遅延読み込みする
        private void GridView_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                if (args.Item is VideoItem itemToUnload)
                {
                    // メモリを解放するためにBitmapImageをクリア
                    itemToUnload.UnloadThumbnailImage();
                }
                return;
            }

            if (args.Item is VideoItem itemToLoad)
            {
                // まだBitmapImageが読み込まれていない、かつ元データが存在する場合のみ、非同期読み込みを開始
                if (itemToLoad.ThumbnailImage == null && itemToLoad.Thumbnail != null)
                {
                    // RegisterUpdateCallbackはUIスレッドでコールバックを実行する
                    args.RegisterUpdateCallback(async (s, e) =>
                    {
                        // UIスレッドで非同期に画像を読み込んで設定する
                        await itemToLoad.LoadThumbnailImageAsync();
                    });
                }
            }
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
    }
}