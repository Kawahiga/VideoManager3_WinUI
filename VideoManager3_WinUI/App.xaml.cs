/* App.xaml.cs */
using Microsoft.UI.Xaml;
using System;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VideoManager3_WinUI
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        internal static Window? m_window;

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            this.InitializeComponent();
        }

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            m_window = new MainWindow();
            // MainWindowのインスタンスを静的プロパティに格納
            MainWindow = (MainWindow)m_window;
            m_window.Activate();
        }

        // アプリケーション全体からMainWindowにアクセスするための静的プロパティを追加
        public static MainWindow? MainWindow { get; private set; }
    }

    // WinRT.Interop.WindowNativeへの拡張メソッドを定義
    public static class WindowExtensions
    {
        public static IntPtr GetWindowHandle(this Window window)
        {
            return WinRT.Interop.WindowNative.GetWindowHandle(window);
        }
    }
}
