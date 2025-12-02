using FFMpegCore;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

namespace VideoManager3_WinUI.Services {
    public class ThumbnailService {

        // サムネイルをキャッシュするフォルダのパス
        private string TempCacheFolder = Path.Combine(ApplicationData.Current.LocalCacheFolder.Path, "Thumbnails");

        // 【暫定】将来的には設定で変更可能にしたい
        // サムネイルを作成する秒数
        private const double ThumbnailWait = 5.0;

        private int count = 0;
        private readonly int limit = 400; // サムネイル生成の制限回数

        // サムネイル画像を生成し、バイト配列として返す
        public async Task<byte[]?> GetThumbnailBytesAsync( string videoPath ) {
            if ( string.IsNullOrEmpty( videoPath ) || !File.Exists( videoPath ) )                 return null;

            if ( !Directory.Exists( TempCacheFolder ) )                 Directory.CreateDirectory( TempCacheFolder );

            // ファイルパスからキャッシュキーを生成
            string tempThumbnailPath = Path.Combine(TempCacheFolder, GetCacheKey(videoPath));

            if ( File.Exists( tempThumbnailPath ) )                 // キャッシュが存在する場合はそれを返す
                return await File.ReadAllBytesAsync( tempThumbnailPath );

            if ( count++ > limit )                 return null; // 【暫定処理】制限回以上呼び出された場合はもうサムネイル作らない

            try {
                await FFMpeg.SnapshotAsync( videoPath, tempThumbnailPath, captureTime: TimeSpan.FromSeconds( ThumbnailWait ) );

                if ( !File.Exists( tempThumbnailPath ) )                     return null;

                // 一時ファイルをバイト配列として読み込む
                var imageBytes = await File.ReadAllBytesAsync(tempThumbnailPath);
                return imageBytes;
            }
            catch ( Exception ex ) {
                System.Diagnostics.Debug.WriteLine( $"Thumbnail generation failed for {videoPath}: {ex.Message}" );
                return null;
            }
        }

        // 動画ファイルのパスからSHA256ハッシュを生成し、キャッシュキーとして返す
        private string GetCacheKey( string videoPath ) {
            // SHA256でハッシュ値を計算
            using ( var sha256 = SHA256.Create() ) {
                var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(videoPath));
                // ハッシュ値を16進数の文字列に変換
                var sb = new StringBuilder();
                foreach ( var b in hashBytes ) {
                    sb.Append( b.ToString( "x2" ) );
                }
                // 拡張子を追加して返す
                return sb.ToString() + ".png";
            }
        }
    }
}
