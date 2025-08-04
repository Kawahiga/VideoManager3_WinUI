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
        private async void TagEdit(object sender, RoutedEventArgs e)
        {
            ViewModel.EditTagCommand.Execute(null);
        }
    }
}