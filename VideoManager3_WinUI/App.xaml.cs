using Microsoft.UI.Xaml;

namespace VideoManager3_WinUI
{
    public partial class App : Application
    {
        // ウィンドウインスタンスを保持する変数。internal staticに変更してどこからでもアクセス可能に。
        internal static Window? m_window;

        public App()
        {
            this.InitializeComponent();
        }

        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            m_window = new MainWindow();
            m_window.Activate();
        }
    }
}
