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
    }
}