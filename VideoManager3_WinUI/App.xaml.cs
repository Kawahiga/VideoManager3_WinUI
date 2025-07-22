using Microsoft.UI.Xaml;
using System.IO;
using Windows.Storage;

namespace VideoManager3_WinUI
{
    public partial class App : Application
    {
        public static DatabaseService Database { get; private set; }

        public App()
        {
            this.InitializeComponent();
        }

        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            // アプリケーション起動時にデータベースサービスを初期化する
            InitializeDatabase();

            m_window = new MainWindow();
            m_window.Activate();
        }

        private void InitializeDatabase()
        {
            // アプリケーションのローカルフォルダにデータベースファイルを保存する
            var dbPath = Path.Combine(ApplicationData.Current.LocalFolder.Path, "videos.db");
            Database = new DatabaseService(dbPath);
        }

        private Window m_window;
    }
}
