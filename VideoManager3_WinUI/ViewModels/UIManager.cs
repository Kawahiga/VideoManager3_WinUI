using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace VideoManager3_WinUI {
    public partial class UIManager : ObservableObject {
        private bool _isGridView = true;
        public bool IsGridView {
            get => _isGridView;
            set {
                if ( SetProperty( ref _isGridView, value ) ) {
                    OnPropertyChanged( nameof( IsListView ) );
                }
            }
        }

        public bool IsListView => !IsGridView;

        private bool _isTreeView = true;
        public bool IsTreeView {
            get => _isTreeView;
            set {
                if ( SetProperty( ref _isTreeView, value ) ) {
                    OnPropertyChanged( nameof( IsArtistView ) );
                }
            }
        }
        public bool IsArtistView => !IsTreeView;

        private double _thumbnailSize = 150.0;
        public double ThumbnailSize {
            get => _thumbnailSize;
            set {
                if ( SetProperty( ref _thumbnailSize, value ) ) {
                    OnPropertyChanged( nameof( ThumbnailHeight ) );
                }
            }
        }
        public double ThumbnailHeight => ThumbnailSize * 9.0 / 16.0;

        [RelayCommand]
        private void ToggleView() {
            IsGridView = !IsGridView;
        }

        public async Task ShowMessageDialogAsync( string title, string message ) {
            if ( App.m_window == null ) {
                return;
            }

            // メッセージの最も長い行の文字数に基づいて幅を計算
            var lines = message.Split('\n');
            int maxLineLength = lines.Any() ? lines.Max(line => line.Length) : 0;

            // 定数（フォントサイズやパディングに応じて調整）
            const double pixelsPerChar = 9.0; // 1文字あたりの平均ピクセル幅
            const double horizontalPadding = 120.0; // ダイアログの左右の余白合計
            const double minWidth = 600.0;
            const double maxWidth = 1200.0;

            // 幅を計算し、最小・最大幅の範囲に収める
            double calculatedWidth = (maxLineLength * pixelsPerChar) + horizontalPadding;
            double finalWidth = Math.Clamp(calculatedWidth, minWidth, maxWidth);

            var scrollViewer = new ScrollViewer
            {
                Content = new TextBlock
                {
                    Text = message,
                    TextWrapping = TextWrapping.Wrap,
                    IsTextSelectionEnabled = true
                },
                MaxHeight = 400
            };

            ContentDialog dialog = new ContentDialog
            {
                Title = title,
                Content = scrollViewer,
                CloseButtonText = "OK",
                XamlRoot = App.m_window.Content.XamlRoot,
                Width = finalWidth // 計算した幅を適用
            };
            await dialog.ShowAsync();
        }

        /// <summary>
        /// 確認ダイアログを表示します。
        /// </summary>
        /// <param name="title">ダイアログのタイトル</param>
        /// <param name="message">表示するメッセージ</param>
        /// <param name="primaryButtonText">プライマリボタンのテキスト（例: "OK", "はい"）</param>
        /// <param name="closeButtonText">閉じるボタンのテキスト（例: "キャンセル", "いいえ"）</param>
        /// <returns>プライマリボタンが押された場合は true、それ以外の場合は false。</returns>
        public async Task<bool> ShowConfirmationDialogAsync(string title, string message, string primaryButtonText = "OK", string closeButtonText = "キャンセル")
        {
            if (App.m_window == null)
            {
                return false;
            }

            ContentDialog dialog = new ContentDialog
            {
                Title = title,
                Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap }, // TextBlockでラップして折り返しを有効に
                PrimaryButtonText = primaryButtonText,
                CloseButtonText = closeButtonText,
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = App.m_window.Content.XamlRoot
            };

            var result = await dialog.ShowAsync();
            return result == ContentDialogResult.Primary;
        }
    }
}
