using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace VideoManager3_WinUI
{
    public sealed partial class MainWindow : Window
    {
        public MainViewModel ViewModel { get; }

        public MainWindow()
        {
            this.InitializeComponent();
            this.Title = "動画管理くん";

            ViewModel = new MainViewModel(DispatcherQueue.GetForCurrentThread());
            // Window.ContentをFrameworkElementにキャストしてDataContextを設定
            (this.Content as FrameworkElement)!.DataContext = ViewModel;

        }


        // タグを編集を実行するイベントハンドラー（ツリー選択時とファイル選択時で共用できる？）
        private async void TagEdit(object sender, RoutedEventArgs e)
        {
            ViewModel.EditTagCommand.Execute(null);
        }

        // ★追加：タグ編集フライアウトが開かれる直前のイベントハンドラ
        private void TagEditFlyout_Opening(object sender, object e)
        {
            // ViewModelに、現在選択中の動画に合わせてタグのチェック状態を更新させる
            ViewModel.PrepareTagsForEditing();

            if (sender is MenuFlyout flyout)
            {
                // 既存の項目をクリア
                flyout.Items.Clear();
                // ViewModelのTagItemsからメニューを再帰的に構築
                CreateTagMenuItems(flyout.Items, ViewModel.TagItems);
            }
        }

        // ★追加：タグ情報から動的にメニュー項目を生成する再帰メソッド
        private void CreateTagMenuItems(IList<MenuFlyoutItemBase> menuItems, IEnumerable<TagItem> tagItems)
        {
            foreach (var tag in tagItems)
            {
                if (tag.IsGroup)
                {
                    // タグがグループの場合、サブメニューを作成
                    var subItem = new MenuFlyoutSubItem { Text = tag.Name };
                    CreateTagMenuItems(subItem.Items, tag.Children);
                    menuItems.Add(subItem);
                }
                else
                {
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
                    menuItems.Add(toggleItem);
                }
            }
        }

        // ★追加：チェックボックス付きメニュー項目がクリックされたときのイベントハンドラ
        private void ToggleTag_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleMenuFlyoutItem toggleItem && toggleItem.Tag is TagItem tagItem)
            {
                // IsCheckedプロパティはクリック後に更新されるため、現在の値を反転させた値をViewModelに伝える
                // ただし、ToggleMenuFlyoutItemがクリックされるとIsCheckedは自動でトグルするので、
                // tagItemのIsCheckedプロパティをUIと同期させてからコマンドを実行する
                tagItem.IsChecked = toggleItem.IsChecked;
                ViewModel.UpdateVideoTagsCommand.Execute(tagItem);
            }
        }
    }
}