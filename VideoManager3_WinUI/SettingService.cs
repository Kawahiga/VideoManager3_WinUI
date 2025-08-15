using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.Storage;
using static VideoManager3_WinUI.VideoService;

// ソート条件やUIの状態（各ペインの幅やサムネイルの拡大状態など）を保存・復元する。
// Windows.Storage.ApplicationData.Current.LocalSettings

namespace VideoManager3_WinUI {

    public class SettingItem {
        public int VideoSortType { get; set; }
        public double[] PaneWidths { get; set; }
        public double ThumbnailSize { get; set; }
        public string HomeFolderPath { get; set; }
        public bool IsGridView { get; set; }

        public SettingItem() {
            VideoSortType = 0;
            PaneWidths = new double[] { 200, 200, 200 };
            ThumbnailSize = 260.0;
            //HomeFolderPath = string.Empty;
            HomeFolderPath = "aaa";
            IsGridView = true;
        }

    }

    internal class SettingService {
        private const string SETTINGS_KEY = "AppSettings";
        private ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;

        public void SaveSettings( SettingItem settings ) {
            if ( settings.HomeFolderPath != null ) {
                settings.HomeFolderPath = settings.HomeFolderPath.Replace( @"\\", @"\" );
            }
            string json = JsonSerializer.Serialize(settings);
            System.Diagnostics.Debug.WriteLine($"Saving settings JSON: {json}");
            localSettings.Values[SETTINGS_KEY] = json;
        }

        public SettingItem LoadSettings() {
            if ( localSettings.Values.TryGetValue( SETTINGS_KEY, out object obj ) && obj is string json ) {
                System.Diagnostics.Debug.WriteLine($"Loading settings JSON: {json}");
                try {
                    var settings = JsonSerializer.Deserialize<SettingItem>( json );
                    //if ( settings.HomeFolderPath != null ) {
                    //    settings.HomeFolderPath = settings.HomeFolderPath.Replace( @"\\", @"\" );
                    //    SaveSettings( settings ); // 修正後のパスを保存
                    //}
                    return settings;
                }
                catch ( JsonException ) {
                    // JSONのデシリアライズに失敗した場合はデフォルト設定を返す
                    return new SettingItem();
                }
            }
            return new SettingItem();
        }
    }
}