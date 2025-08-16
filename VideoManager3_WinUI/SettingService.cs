using System.Text.Json;
using Windows.Storage;

// ソート条件やUIの状態（各ペインの幅やサムネイルの拡大状態など）を保存・復元する。

namespace VideoManager3_WinUI {

    public class SettingItem {
        public int VideoSortType { get; set; }      // 動画のソート条件
        public double[] PaneWidths { get; set; }    // 各ペインの幅（左ペイン、中央ペイン、右ペイン）
        public double ThumbnailSize { get; set; }   // サムネイルのサイズ
        public string HomeFolderPath { get; set; }  // ホームフォルダのパス
        public bool IsGridView { get; set; }        // グリッドビューかリストビューかの状態
        public bool IsFullScreen { get; set; }      // フルスクリーンモードの状態
        public double[] WindowPosition { get; set; } // ウィンドウの位置（X, Y）

        public SettingItem() {
            VideoSortType = 0;
            PaneWidths = [200, 200, 200];
            ThumbnailSize = 260.0;
            HomeFolderPath = string.Empty;
            IsGridView = true;
            IsFullScreen = false;
            WindowPosition = [100, 100];
        }

    }

    internal class SettingService {
        private const string SETTINGS_KEY = "AppSettings";
        private ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;

        /// <summary>
        /// 設定の保存
        /// <!summary>
        public void SaveSettings( SettingItem settings ) {
            string json = JsonSerializer.Serialize(settings);
            localSettings.Values[SETTINGS_KEY] = json;
        }

        /// <summary>
        /// 設定の読み込み
        /// <!summary>
        public SettingItem LoadSettings() {
            if ( localSettings.Values.TryGetValue( SETTINGS_KEY, out object? obj ) && obj is string json ) {
                try {
                    var settings = JsonSerializer.Deserialize<SettingItem>( json );
                    if ( settings != null ) return settings;

                } catch ( JsonException ) {
                    // JSONのデシリアライズに失敗した場合はデフォルト設定を返す
                    return new SettingItem();
                }
            }
            return new SettingItem();
        }
    }
}