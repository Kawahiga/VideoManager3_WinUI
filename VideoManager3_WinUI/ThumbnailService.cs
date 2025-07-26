using FFMpegCore;
using System;
using System.IO;
using System.Threading.Tasks;

namespace VideoManager3_WinUI
{
    public class ThumbnailService
    {
        // 戻り値をBitmapImageから画像のバイト配列(byte[]?)に変更
        public async Task<byte[]?> GetThumbnailBytesAsync(string videoPath)
        {
            if (string.IsNullOrEmpty(videoPath) || !File.Exists(videoPath))
            {
                return null;
            }

            string tempThumbnailPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.png");

            try
            {
                await FFMpeg.SnapshotAsync(videoPath, tempThumbnailPath, captureTime: TimeSpan.FromSeconds(1));

                if (!File.Exists(tempThumbnailPath))
                {
                    return null;
                }

                // 一時ファイルをバイト配列として読み込む
                var imageBytes = await File.ReadAllBytesAsync(tempThumbnailPath);
                return imageBytes;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Thumbnail generation failed for {videoPath}: {ex.Message}");
                return null;
            }
            finally
            {
                if (File.Exists(tempThumbnailPath))
                {
                    File.Delete(tempThumbnailPath);
                }
            }
        }
    }
}
