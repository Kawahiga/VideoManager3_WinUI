using FFMpegCore;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;

namespace VideoManager3_WinUI.Services {
    public class ThumbnailService {
        private static readonly SemaphoreSlim _semaphore = new SemaphoreSlim(4); // 同時実行数を4に制限
        private static readonly Random _random = new Random();
        private const int NumberOfCandidates = 5; // 生成するサムネイル候補の数
        
        // GIFの仕様: 3秒間, 10fps, 幅320px
        private static readonly int _gifDurationSeconds = 3; // GIFの再生時間
        private static readonly int _gifFps = 10;   // GIFのフレームレート
        private static readonly int _thamSize = 640; // サムネイルの幅（png,gif共通）

        // サムネイルをキャッシュするパス
        private readonly string TempCacheFolder = Path.Combine(ApplicationData.Current.LocalCacheFolder.Path, "Thumbnails");

        /// <summary>
        /// サムネイル画像をバイト配列として取得する 
        /// </summary>
        public async Task<byte[]?> GetThumbnailBytesAsync( string videoPath ) {
            if ( string.IsNullOrEmpty( videoPath ) || !File.Exists( videoPath ) ) {
                return null;
            }

            if ( !Directory.Exists( TempCacheFolder ) ) {
                Directory.CreateDirectory( TempCacheFolder );
            }

            string thumbnailPath = Path.Combine(TempCacheFolder, GetCacheKey(videoPath, ".png"));

            if ( File.Exists( thumbnailPath ) ) {
                return await File.ReadAllBytesAsync( thumbnailPath );
            }

            // キャッシュが存在しない場合は作成する
            return await CreateThumbnailAsync( videoPath );
        }

        /// <summary>
        /// サムネイル画像を強制的に再生成し、バイト配列として返す
        /// </summary>
        public async Task<byte[]?> CreateThumbnailAsync( string videoPath ) {
            if ( string.IsNullOrEmpty( videoPath ) || !File.Exists( videoPath ) ) {
                return null;
            }

            if ( !Directory.Exists( TempCacheFolder ) ) {
                Directory.CreateDirectory( TempCacheFolder );
            }

            string finalThumbnailPath = Path.Combine(TempCacheFolder, GetCacheKey(videoPath, ".png"));

            // 既存のキャッシュファイルがあれば削除する
            if ( File.Exists( finalThumbnailPath ) ) {
                try {
                    File.Delete( finalThumbnailPath );
                } catch ( Exception ex ) {
                    System.Diagnostics.Debug.WriteLine( $"Failed to delete existing thumbnail cache {finalThumbnailPath}: {ex.Message}" );
                    // 削除に失敗しても処理を続行する
                }
            }

            await _semaphore.WaitAsync();
            try {
                await FindBestThumbnailAsync( videoPath, finalThumbnailPath );

                if ( !File.Exists( finalThumbnailPath ) ) {
                    return null;
                }

                return await File.ReadAllBytesAsync( finalThumbnailPath );
            } catch ( Exception ex ) {
                System.Diagnostics.Debug.WriteLine( $"Thumbnail generation failed for {videoPath}: {ex.Message}" );
                return null;
            } finally {
                _semaphore.Release();
            }
        }

        // 最適なサムネイルを見つけて保存する
        private async Task FindBestThumbnailAsync( string videoPath, string finalThumbnailPath ) {
            var mediaInfo = await FFProbe.AnalyseAsync(videoPath);
            var duration = mediaInfo.Duration;
            var candidatePaths = new List<string>();
            var bestCandidate = (Path: (string?)null, Score: -1.0);

            string tempFolderName = $"temp_{Path.GetRandomFileName()}";
            string tempFolderForCandidates = Path.Combine(TempCacheFolder, tempFolderName);
            Directory.CreateDirectory( tempFolderForCandidates );

            try {
                // 候補を生成
                for ( int i = 0; i < NumberOfCandidates; i++ ) {
                    // 動画の10%から90%の間でランダムな時間を指定
                    double randomPoint = _random.NextDouble() * 0.8 + 0.1;
                    var captureTime = TimeSpan.FromSeconds(duration.TotalSeconds * randomPoint);

                    string candidatePath = Path.Combine(tempFolderForCandidates, $"{i}.png");
                    await FFMpegArguments
                        .FromFileInput( videoPath, true, options => options
                        .Seek( captureTime ) ) // ここもInput Seekにして高速化
                        .OutputToFile( candidatePath, true, options => options
                        .WithCustomArgument( "-vframes 1" )
                        .WithCustomArgument( "-vf scale=" + _thamSize + ":-1:flags=bilinear" ) )
                        .ProcessAsynchronously();

                    if ( File.Exists( candidatePath ) ) {
                        candidatePaths.Add( candidatePath );
                    }
                }

                if ( candidatePaths.Count == 0 ) {
                    // 候補が一つも生成されなかった場合、古い方法にフォールバックする
                    await FFMpeg.SnapshotAsync( videoPath, finalThumbnailPath, captureTime: TimeSpan.FromSeconds( 5 ) );
                    return;
                }


                // 候補を評価し、最適なものを選択する
                foreach ( var path in candidatePaths ) {
                    var score = await CalculateImageScoreAsync(path);
                    if ( score > bestCandidate.Score ) {
                        bestCandidate = (path, score);
                    }
                }

                // 最適な候補を最終的な保存先にコピーする
                if ( bestCandidate.Path != null ) {
                    File.Copy( bestCandidate.Path, finalThumbnailPath, true );
                }
            } finally {
                // 一時的な候補ファイルとフォルダをクリーンアップする
                if ( Directory.Exists( tempFolderForCandidates ) ) {
                    Directory.Delete( tempFolderForCandidates, true );
                }
            }
        }

        // 画像の品質スコアを計算する
        private async Task<double> CalculateImageScoreAsync( string imagePath ) {
            try {
                using var image = await Image.LoadAsync<Rgba32>(imagePath);

                // 明るさ、コントラスト、色彩の豊かさを計算する
                double totalLuminance = 0;
                var luminances = new List<double>();
                var colors = new HashSet<Rgba32>();

                image.ProcessPixelRows( accessor => {
                    for ( int y = 0; y < accessor.Height; y += 10 ) { // y++ を y += 10 に変更（行を間引く）
                        var row = accessor.GetRowSpan(y);
                        for ( int x = 0; x < row.Length; x += 10 ) { // x++ を x += 10 に変更（列を間引く）
                            var pixel = row[x];

                            // 輝度計算 (処理回数が1/100になるため高速)
                            double luminance = 0.299 * pixel.R + 0.587 * pixel.G + 0.114 * pixel.B;
                            totalLuminance += luminance;
                            luminances.Add( luminance );

                            // 色のサンプリング（条件分岐なしで常に追加できる）
                            colors.Add( pixel );
                        }
                    }
                } );

                double avgLuminance = totalLuminance / luminances.Count;
                double stdDev = Math.Sqrt(luminances.Average(l => Math.Pow(l - avgLuminance, 2)));

                // 値を0-1のスケールに正規化する
                double brightnessScore = 1 - Math.Abs(avgLuminance / 255.0 - 0.5) * 2; // 暗すぎる/明るすぎる画像にペナルティを与える
                double contrastScore = stdDev / 128.0; // 標準偏差が大きいほどコントラストが高い
                double colorScore = Math.Min(colors.Count / 500.0, 1.0); // 想定される色数に基づいて正規化する

                // 重みは調整可能
                return (brightnessScore * 0.3) + (contrastScore * 0.5) + (colorScore * 0.2);
            } catch ( Exception ex ) {
                System.Diagnostics.Debug.WriteLine( $"Failed to calculate score for {imagePath}: {ex.Message}" );
                return -1; // 失敗を示すスコアを返す
            }
        }


        // 動画パスからSHA256ハッシュを生成し、キャッシュキーとして使用する
        private string GetCacheKey( string videoPath, string extension ) {
            using ( var sha256 = SHA256.Create() ) {
                var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(videoPath));
                var sb = new StringBuilder();
                foreach ( var b in hashBytes ) {
                    sb.Append( b.ToString( "x2" ) );
                }
                return sb.ToString() + extension;
            }
        }

        /// <summary>
        /// プレビュー用のGIFを生成し、そのファイルパスを返す
        /// </summary>
        public async Task<string?> CreatePreviewGifAsync( string videoPath ) {
            if ( string.IsNullOrEmpty( videoPath ) || !File.Exists( videoPath ) ) {
                return null;
            }

            if ( !Directory.Exists( TempCacheFolder ) ) {
                Directory.CreateDirectory( TempCacheFolder );
            }

            string gifPath = Path.Combine(TempCacheFolder, GetCacheKey(videoPath, ".gif"));

            // 既にGIFキャッシュが存在する場合はそのパスを返す
            if ( File.Exists( gifPath ) ) {
                return gifPath;
            }

            await _semaphore.WaitAsync();
            try {
                var mediaInfo = await FFProbe.AnalyseAsync(videoPath);
                var duration = mediaInfo.Duration;
                var startTime = TimeSpan.FromSeconds(duration.TotalSeconds * 0.45);

                await FFMpegArguments
                    .FromFileInput( videoPath, true, options => options
                        .Seek( startTime ) ) // ← 入力側でシークすることで、一瞬でジャンプします
                    .OutputToFile( gifPath, true, options => options
                        .WithCustomArgument( "-vf \"fps=" + _gifFps + ",scale=" + _thamSize + ":-1:flags=bilinear\"" ) // bilinearに変更
                        .WithDuration( TimeSpan.FromSeconds( _gifDurationSeconds ) ) )
                    .ProcessAsynchronously();

                if ( File.Exists( gifPath ) ) {
                    return gifPath;
                }
                return null;
            } catch ( Exception ex ) {
                System.Diagnostics.Debug.WriteLine( $"Preview GIF generation failed for {videoPath}: {ex.Message}" );
                return null;
            } finally {
                _semaphore.Release();
            }
        }

        /// <summary>
        /// 指定されたパスに対応するキャッシュ済みのプレビューGIFが存在するか確認し、パスを返します。
        /// 既存のGIFファイルのみを返し、新規作成は行いません。
        /// </summary>
        public string? GetExistingPreviewGifPathIfExists( string videoPath ) {
            if ( string.IsNullOrEmpty( videoPath ) ) {
                return null;
            }

            if ( !Directory.Exists( TempCacheFolder ) ) {
                return null;
            }

            string gifPath = Path.Combine(TempCacheFolder, GetCacheKey(videoPath, ".gif"));

            return File.Exists( gifPath ) ? gifPath : null;
        }

        /// <summary>
        /// データベースに存在しない動画に対応する、古いサムネイルキャッシュファイルを削除します。
        /// </summary>
        public Task<int> DeleteOrphanedThumbnailsAsync( IEnumerable<string> videoPaths ) {
            return Task.Run( () => {
                int deleteCount = 0;
                try {
                    if ( !Directory.Exists( TempCacheFolder ) ) {
                        return 0;
                    }

                    var validCacheKeys = new HashSet<string>(videoPaths.Select(p => GetCacheKey(p, ".png")));
                    var validGifCacheKeys = new HashSet<string>(videoPaths.Select(p => GetCacheKey(p, ".gif")));

                    // キャッシュフォルダ内のすべてのファイルを取得
                    var cachedFiles = Directory.GetFiles(TempCacheFolder);

                    foreach ( var file in cachedFiles ) {
                        var fileName = Path.GetFileName(file);
                        if ( !validCacheKeys.Contains( fileName ) && !validGifCacheKeys.Contains( fileName ) ) {
                            try {
                                File.Delete( file );
                                deleteCount++;
                                System.Diagnostics.Debug.WriteLine( $"Deleted orphaned thumbnail: {fileName}" );
                            } catch ( Exception ex ) {
                                System.Diagnostics.Debug.WriteLine( $"Failed to delete thumbnail {fileName}: {ex.Message}" );
                            }
                        }
                    }
                } catch ( Exception ex ) {
                    System.Diagnostics.Debug.WriteLine( $"Error deleting orphaned thumbnails: {ex.Message}" );
                }
                return deleteCount;
            } );
        }
    }
}
