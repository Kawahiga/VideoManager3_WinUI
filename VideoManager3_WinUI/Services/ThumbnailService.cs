using FFMpegCore;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;

namespace VideoManager3_WinUI.Services {
    public class ThumbnailService {
        private static readonly SemaphoreSlim _semaphore = new SemaphoreSlim(4); // 同時実行数を4に制限
        private const long MinFileSize = 200 * 1024; // 200KB
        private const int MaxRetryTime = 30; // 最大リトライ時間（秒）
        private const int RetryInterval = 5; // リトライ間隔（秒）

        // サムネイルをキャッシュするフォルダのパス
        private readonly string TempCacheFolder = Path.Combine(ApplicationData.Current.LocalCacheFolder.Path, "Thumbnails");

        // サムネイル画像を生成し、バイト配列として返す
        public async Task<byte[]?> GetThumbnailBytesAsync( string videoPath ) {
            if ( string.IsNullOrEmpty( videoPath ) || !File.Exists( videoPath ) ) {
                return null;
            }

            if ( !Directory.Exists( TempCacheFolder ) ) {
                Directory.CreateDirectory( TempCacheFolder );
            }

            string tempThumbnailPath = Path.Combine(TempCacheFolder, GetCacheKey(videoPath));

            if ( File.Exists( tempThumbnailPath ) ) {
                return await File.ReadAllBytesAsync( tempThumbnailPath );
            }

            await _semaphore.WaitAsync();
            try {
                var mediaInfo = await FFProbe.AnalyseAsync(videoPath);
                var duration = mediaInfo.Duration;

                for ( int time = RetryInterval; time <= MaxRetryTime; time += RetryInterval ) {
                    if ( duration < TimeSpan.FromSeconds( time ) ) {
                        // キャプチャ試行時間が動画の長さを超える場合は、前の時間のサムネイルで確定
                        break;
                    }

                    await FFMpeg.SnapshotAsync( videoPath, tempThumbnailPath, captureTime: TimeSpan.FromSeconds( time ) );

                    if ( File.Exists( tempThumbnailPath ) ) {
                        var fileInfo = new FileInfo(tempThumbnailPath);
                        if ( fileInfo.Length >= MinFileSize ) {
                            // ファイルサイズが閾値以上ならループを抜ける
                            break;
                        }
                    }
                }

                if ( !File.Exists( tempThumbnailPath ) ) {
                    return null;
                }

                return await File.ReadAllBytesAsync( tempThumbnailPath );
            } catch ( Exception ex ) {
                System.Diagnostics.Debug.WriteLine( $"Thumbnail generation failed for {videoPath}: {ex.Message}" );
                return null;
            } finally {
                _semaphore.Release();
            }
        }

        // 動画ファイルのパスからSHA256ハッシュを生成し、キャッシュキーとして返す
        private string GetCacheKey( string videoPath ) {
            using ( var sha256 = SHA256.Create() ) {
                var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(videoPath));
                var sb = new StringBuilder();
                foreach ( var b in hashBytes ) {
                    sb.Append( b.ToString( "x2" ) );
                }
                return sb.ToString() + ".png";
            }
        }
    }
}
