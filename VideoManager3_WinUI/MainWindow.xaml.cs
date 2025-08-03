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

  //      public ObservableCollection<TagItem> TagRootItems { get; set; }

        public MainWindow()
        {
            this.InitializeComponent();
            this.Title = "動画管理くん";

            ViewModel = new MainViewModel(DispatcherQueue.GetForCurrentThread());
            // Window.ContentをFrameworkElementにキャストしてDataContextを設定
            (this.Content as FrameworkElement)!.DataContext = ViewModel;

            // タグの初期化
//            TagRootItems = ViewModel.TagItems;
        }
        private async void Rename_Click(object sender, RoutedEventArgs e)
        {
            // クリックされたメニュー項目から、対象のデータ(FileSystemItem)を取得
            var menuItem = sender as MenuFlyoutItem;
            var itemToRename = menuItem?.DataContext as TagItem;
            if (itemToRename == null) return;

            // 名前変更用のテキストボックスを持つダイアログを作成
            var renameTextBox = new TextBox { Text = itemToRename.Name };
            var dialog = new ContentDialog
            {
                Title = "名前の変更",
                Content = renameTextBox,
                PrimaryButtonText = "変更",
                CloseButtonText = "キャンセル",
                
                // XamlRoot = App.MainWindow.Content.XamlRoot // ダイアログを表示するために必要
                XamlRoot = this.Content.XamlRoot // ダイアログを表示するために必要
            };

            var result = await dialog.ShowAsync();

            // 「変更」ボタンが押されたら、Nameプロパティを更新
            if (result == ContentDialogResult.Primary)
            {
                // FileSystemItemがINotifyPropertyChangedを実装しているため、
                // このプロパティ変更は自動的にUIに反映されます。
                itemToRename.Name = renameTextBox.Text;
            }
        }
    }
}