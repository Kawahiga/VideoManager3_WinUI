using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;
using WinRT.Interop;

namespace VideoManager3_WinUI
{
    public sealed partial class MainWindow : Window
    {
        public MainViewModel ViewModel { get; }

        public MainWindow()
        {
            this.InitializeComponent();
            ViewModel = new MainViewModel();
            
            // ViewModelからダイアログ表示の要求があった際の処理を、ここで定義して設定する
            ViewModel.ShowAddTagDialogAsync = ShowAddTagDialogAsync;

            // ViewModelにウィンドウハンドルを渡す
            ViewModel.Initialize(WindowNative.GetWindowHandle(this));
        }

        // ViewModelから呼び出される、タグ追加ダイアログを表示するためのメソッド
        private async Task<string?> ShowAddTagDialogAsync()
        {
            var textBox = new TextBox { PlaceholderText = "新しいタグ名" };
            var dialog = new ContentDialog
            {
                Title = "タグの追加",
                Content = textBox,
                PrimaryButtonText = "追加",
                CloseButtonText = "キャンセル",
                // このXamlRootは、このメソッドが呼ばれる時点では常に有効
                XamlRoot = this.Content.XamlRoot, 
                DefaultButton = ContentDialogButton.Primary
            };

            var result = await dialog.ShowAsync();

            // ユーザーが「追加」を押し、テキストが空でなければそのテキストを返す
            if (result == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(textBox.Text))
            {
                return textBox.Text;
            }
            
            // それ以外の場合はnullを返す
            return null;
        }

        public Visibility ConvertObjectToVisibility(object? obj)
        {
            return obj != null ? Visibility.Visible : Visibility.Collapsed;
        }

        public string GetItemName(VideoItem? item)
        {
            return item?.Name ?? string.Empty;
        }

        public string GetItemPath(VideoItem? item)
        {
            return item?.Path ?? string.Empty;
        }

        public string FormatLastModified(VideoItem? item)
        {
            if (item != null)
            {
                return $"更新日時: {item.LastModified:yyyy/MM/dd HH:mm}";
            }
            return string.Empty;
        }
    }
}

