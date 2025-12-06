using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using VideoManager3_WinUI.Models;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace VideoManager3_WinUI.Services {
    public enum VideoSortType {
        LastModifiedDescending, // 更新日時降順
        LastModifiedAscending,  // 更新日時昇順
        FileNameAscending,      // ファイル名昇順
        FileNameDescending,      // ファイル名降順
        FileSizeAscending,     // ファイルサイズ昇順
        FileSizeDescending,    // ファイルサイズ降順
        LikeCountDescending, // いいね数降順
        LikeCountAscending,  // いいね数昇順
    }

    public enum RenameResult {
        Success,         // 成功
        AlreadyExists,   // 同名ファイルが既に存在する
        AccessDenied,    // アクセスが拒否された
        FileInUse,       // ファイルが使用中
        InvalidName,     // ファイル名が不正
        UnknownError     // その他のエラー
    }

    /// <summary>
    /// 動画関連のデータ操作とビジネスロジックを管理するサービスクラス
    /// </summary>
    public class VideoService {
        public ObservableCollection<VideoItem> Videos { get; } = new ObservableCollection<VideoItem>();

        public string? HomeFolder = null;

        private readonly DatabaseService _databaseService;
        private readonly TagService _tagService;
        private readonly ThumbnailService _thumbnailService;

        public VideoService( DatabaseService databaseService, TagService tagService, ThumbnailService thumbnailService ) {
            _databaseService = databaseService;
            _tagService = tagService;
            _thumbnailService = thumbnailService;
        }

        /// <summary>
        /// データベースから動画を非同期にロードし、UIを更新します。
        /// </summary>
        public async Task LoadVideosAsync() {
            Videos.Clear();
            var videosFromDb = await _databaseService.GetAllVideosAsync();

            foreach ( var video in videosFromDb ) {
                Videos.Add( video );
                // サムネイルのbyte[]の読み込みをバックグラウンドで実行
                _ = Task.Run( () => LoadThumbnailBytesAsync( video ) );
            }
        }

        /// <summary>
        /// 動画のサムネイル(byte[])を非同期で読み込み、VideoItemのプロパティを更新します。
        /// </summary>
        private async Task LoadThumbnailBytesAsync( VideoItem videoItem ) {
            if ( videoItem == null || string.IsNullOrEmpty( videoItem.FilePath ) )
                return;

            var imageBytes = await _thumbnailService.GetThumbnailBytesAsync(videoItem.FilePath);

            if ( imageBytes != null && imageBytes.Length > 0 )
                videoItem.Thumbnail = imageBytes;
        }

        /// <summary>
        /// データベースから各動画に紐づくタグ情報を非同期にロードし、VideoItemのプロパティを更新します。
        /// </summary>
        public async Task LoadVideoTagsAsync( ObservableCollection<TagItem> Tags ) {
            var orderedAllTags = GetTagsInOrder(Tags);
            var allTagsLookup = orderedAllTags.ToDictionary(t => t.Id);

            foreach ( var video in Videos ) {
                video.VideoTagItems.Clear();
                var tagsForVideoFromDb = await _databaseService.GetTagsForVideoAsync(video);
                var tagsForVideoIds = new HashSet<int>(tagsForVideoFromDb.Select(t => t.Id));

                // orderedAllTags の順序を維持しつつ、このビデオに紐づくタグのみをフィルタリングして追加
                foreach ( var tag in orderedAllTags ) {
                    if ( tagsForVideoIds.Contains( tag.Id ) )
                        video.VideoTagItems.Add( tag );
                }
            }
        }
        /// <summary>
        /// すべてのタグをツリーの表示順でフラットなリストとして取得します。
        /// TagTreeViewModelと同じ実装 1本化したい・・・
        /// </summary>
        private List<TagItem> GetTagsInOrder( ObservableCollection<TagItem> TagItems ) {
            var orderedTags = new List<TagItem>();
            void Traverse( IEnumerable<TagItem> tags ) {
                foreach ( var tag in tags ) {
                    orderedTags.Add( tag );
                    Traverse( tag.Children );
                }
            }
            Traverse( TagItems );
            return orderedTags;
        }

        /// <summary>
        /// 選択された動画を削除します。
        /// ファイルを先に削除し、成功した場合にDBとセッションデータを削除します。
        /// </summary>
        /// <returns>削除に成功した場合は true、失敗した場合は false。</returns>
        public async Task<bool> DeleteVideoAsync( VideoItem? videoItem ) {
            if ( videoItem == null || string.IsNullOrEmpty( videoItem.FilePath ) )
                return false;

            // 1. ファイルシステムから削除を試みる
            try {
                var file = await StorageFile.GetFileFromPathAsync(videoItem.FilePath);
                await file.DeleteAsync();
            } catch ( FileNotFoundException ) {
                // ファイルが既に存在しない場合は、成功とみなし、DBからの削除処理に進む
                Debug.WriteLine( $"File not found, but proceeding with DB deletion: {videoItem.FilePath}" );
            } catch ( Exception ex ) {
                // その他のファイル関連エラー（アクセス拒否など）
                Debug.WriteLine( $"Error deleting file: {videoItem.FilePath}. Error: {ex.Message}" );
                // ファイル削除に失敗したため、処理を中断
                return false;
            }

            // 2. データベースとセッションデータから削除
            try {
                // データベースから動画と動画とタグの紐づけ情報を削除
                await _databaseService.DeleteVideoAsync( videoItem );

                // セッションデータから動画を削除
                foreach ( var tag in videoItem.VideoTagItems ) {
                    tag.TagVideoItem.Remove( videoItem );
                }
                foreach ( var artist in videoItem.ArtistsInVideo ) {
                    artist.VideosInArtist.Remove( videoItem );
                }
                Videos.Remove( videoItem );

                return true; // すべての処理が成功
            } catch ( Exception ex ) {
                // DB削除またはセッションデータ操作の失敗
                Debug.WriteLine( $"Error deleting video data from database or session: {videoItem.FilePath}. Error: {ex.Message}" );
                // この時点でファイルは既に削除済みのため、データの不整合が発生している。
                // より堅牢な実装では、このエラーをログに記録し、手動での修正を促すなどの対策が考えられる。
                return false;
            }
        }

        /// <summary>
        /// 選択されたファイル（動画またはフォルダ）を開きます。
        /// ・動画：再生を開始
        /// ・フォルダ：フォルダを開く
        /// </summary>
        public void OpenFile( VideoItem? videoItem ) {
            if ( videoItem == null || string.IsNullOrEmpty( videoItem.FilePath ) )
                return;

            try {
                Process.Start( new ProcessStartInfo( videoItem.FilePath ) { UseShellExecute = true } );
                videoItem.ViewCount++; // 再生数を1増やす
            } catch ( Exception ex ) {
                Debug.WriteLine( $"Error opening file: {ex.Message}" );
            }
        }

        /// <summary>
        /// ファイルをソートします。
        /// </summary>
        public void SortVideos( VideoSortType sortType ) {
            if ( Videos == null || Videos.Count == 0 ) {
                Debug.WriteLine( "No videos to sort." );
                return;
            }
            List<VideoItem> sortedVideos;
            switch ( sortType ) {
                case VideoSortType.LastModifiedDescending:
                sortedVideos = Videos.OrderByDescending( v => v.LastModified ).ToList();
                break;
                case VideoSortType.LastModifiedAscending:
                sortedVideos = Videos.OrderBy( v => v.LastModified ).ToList();
                break;
                case VideoSortType.FileNameAscending:
                sortedVideos = Videos.OrderBy( v => v.FileName ).ToList();
                break;
                case VideoSortType.FileNameDescending:
                sortedVideos = Videos.OrderByDescending( v => v.FileName ).ToList();
                break;
                case VideoSortType.FileSizeAscending:
                sortedVideos = Videos.OrderBy( v => v.FileSize ).ToList();
                break;
                case VideoSortType.FileSizeDescending:
                sortedVideos = Videos.OrderByDescending( v => v.FileSize ).ToList();
                break;
                case VideoSortType.LikeCountDescending:
                sortedVideos = Videos.OrderByDescending( v => v.LikeCount ).ToList();
                break;
                case VideoSortType.LikeCountAscending:
                sortedVideos = Videos.OrderBy( v => v.LikeCount ).ToList();
                break;
                default:
                Debug.WriteLine( $"Unsupported sort type: {sortType}" );
                return;
            }

            Videos.Clear();
            foreach ( var video in sortedVideos ) {
                Videos.Add( video );
            }
        }

        /// <summary>
        /// 指定されたファイル名に一致する重複したVideoItemをDB内から検索します。
        /// </summary>
        /// <param name="fileName">チェックする完全なファイル名。</param>
        /// <param name="fileNameWithoutArtists">チェックするアーティスト名を除いたファイル名。</param>
        /// <param name="excludeVideo">チェック対象から除外するビデオ（オプション）。</param>
        /// <returns>重複するVideoItemのシーケンス。</returns>
        private IEnumerable<VideoItem> GetDuplicateVideos( string fileName, string fileNameWithoutArtists, VideoItem? excludeVideo ) {
            // 比較用の拡張子なしファイル名を作成
            var fileNameToCheck = Path.GetFileNameWithoutExtension(fileName);
            var fileNameWithoutArtistsToCheck = string.IsNullOrEmpty(fileNameWithoutArtists) ? string.Empty : Path.GetFileNameWithoutExtension(fileNameWithoutArtists);
            var idToExclude = excludeVideo?.Id ?? -1; // excludeVideoがnullの場合はどのIDとも一致しない-1を設定

            // 大文字小文字を区別せずに比較
            return Videos.Where( v =>
                v.Id != idToExclude &&
                (
                    // 1. DBのビデオの拡張子を除いたファイル名が、チェック対象のファイル名と一致するか
                    string.Equals( Path.GetFileNameWithoutExtension( v.FileName ), fileNameToCheck, StringComparison.OrdinalIgnoreCase ) ||

                    // 2. DBのビデオのアーティスト名を除いたファイル名（これも拡張子を除く）が、
                    //    チェック対象のアーティスト名を除いたファイル名と、空でなく、かつ一致するか
                    (
                        !string.IsNullOrEmpty( fileNameWithoutArtistsToCheck ) &&
                        !string.IsNullOrEmpty( v.FileNameWithoutArtists ) &&
                        string.Equals( Path.GetFileNameWithoutExtension( v.FileNameWithoutArtists ), fileNameWithoutArtistsToCheck, StringComparison.OrdinalIgnoreCase )
                    )
                )
            );
        }

        /// <summary>
        /// 指定されたファイル名が、指定されたビデオを除き、DB内に既に存在するかどうかを確認します。
        /// FileNameとFileNameWithoutArtistsの両方でチェックを行います。
        /// </summary>
        /// <param name="fileName">チェックする完全なファイル名。</param>
        /// <param name="fileNameWithoutArtists">チェックするアーティスト名を除いたファイル名。</param>
        /// <param name="excludeVideo">チェック対象から除外するビデオ。</param>
        /// <returns>ファイル名が重複している場合は true、それ以外は false。</returns>
        private bool IsFileNameDuplicateInDatabase( string fileName, string fileNameWithoutArtists, VideoItem? excludeVideo ) {
            return GetDuplicateVideos( fileName, fileNameWithoutArtists, excludeVideo ).Any();
        }

        /// <summary>
        /// 動画ファイルの名前を変更します。
        /// </summary>
        /// <returns>ファイル名の変更結果を示す RenameResult。</returns>
        public async Task<RenameResult> RenameFileAsync( VideoItem videoItem, string newFileName, string newFileNameWithoutArtists ) {
            if ( videoItem == null || string.IsNullOrWhiteSpace( videoItem.FilePath ) ) {
                return RenameResult.UnknownError;
            }
            if ( string.IsNullOrWhiteSpace( newFileName ) || newFileName.IndexOfAny( Path.GetInvalidFileNameChars() ) >= 0 ) {
                return RenameResult.InvalidName;
            }
            if ( newFileName.Equals( videoItem.FileName ) ) {
                // 同じ名前の場合はエラーではないが、何もせず成功を返す
                return RenameResult.Success;
            }

            var oldPath = videoItem.FilePath;
            var directory = Path.GetDirectoryName(oldPath);
            if ( string.IsNullOrEmpty( directory ) ) {
                return RenameResult.UnknownError;
            }

            var newPath = Path.Combine(directory, newFileName);
            if ( File.Exists( newPath ) || IsFileNameDuplicateInDatabase( newFileName, newFileNameWithoutArtists, videoItem ) ) {
                return RenameResult.AlreadyExists;
            }


            try {
                File.Move( oldPath, newPath );
                videoItem.FilePath = newPath;
                videoItem.FileName = newFileName;
                videoItem.FileNameWithoutArtists = newFileNameWithoutArtists;
                await _databaseService.UpdateVideoAsync( videoItem );
                return RenameResult.Success;

            } catch ( UnauthorizedAccessException ) {
                Debug.WriteLine( "Error renaming file: Access denied." );
                return RenameResult.AccessDenied;
            } catch ( IOException ex ) when ( (ex.HResult & 0xFFFF) == 0x20 || (ex.HResult & 0xFFFF) == 0x21 ) // ERROR_SHARING_VIOLATION or ERROR_LOCK_VIOLATION
              {
                Debug.WriteLine( "Error renaming file: File in use." );
                return RenameResult.FileInUse;
            } catch ( Exception ex ) {
                Debug.WriteLine( $"Error renaming file: {ex.Message}" );
                return RenameResult.UnknownError;
            }
        }

        /// <summary>
        /// 指定されたフォルダ内のファイルと重複する名前を持つビデオをDB内から検索します。
        /// FileName（完全一致）またはFileNameWithoutArtists（アーティスト名を除いた部分）が一致する場合に重複とみなします。
        /// </summary>
        /// <param name="folderPath">チェック対象のフォルダパス。</param>
        /// <returns>重複が見つかったDB内のVideoItemのリスト。</returns>
        public List<VideoItem> GetDuplicateVideosInFolder( string folderPath ) {
            var duplicateDbVideos = new HashSet<VideoItem>(); // 重複しているDB側のビデオを格納
            if ( !Directory.Exists( folderPath ) ) {
                Debug.WriteLine( $"Folder not found: {folderPath}" );
                return [];
            }

            try {
                var filesInFolder = Directory.GetFiles(folderPath, "*", SearchOption.TopDirectoryOnly);

                foreach ( var filePath in filesInFolder ) {
                    var fileName = Path.GetFileName(filePath);
                    var fileNameWithoutArtists = ArtistService.GetFileNameWithoutArtist(fileName);

                    // DB内のビデオと照合
                    var matches = GetDuplicateVideos(fileName, fileNameWithoutArtists, null);

                    foreach ( var match in matches ) {
                        duplicateDbVideos.Add( match );
                    }
                }

                return duplicateDbVideos.OrderBy( v => v.FileName ).ThenBy( v => v.FilePath ).ToList();
            } catch ( Exception ex ) {
                Debug.WriteLine( $"Error getting duplicate videos: {ex.Message}" );
                return [];
            }
        }

        /// <summary>
        /// 指定されたフォルダ内のファイルを更新日付に合わせたフォルダに移動します。
        /// ただし、DB内に同名ファイルが存在する場合はスキップします。
        /// </summary>
        public async Task<List<VideoItem>> MoveVideosToDateFoldersAsync( string folderPath ) {
            var newVideos = new List<VideoItem>();
            if ( !Directory.Exists( folderPath ) ) {
                Debug.WriteLine( $"[MoveVideosToDateFoldersAsync] Folder not found: {folderPath}" );
                return newVideos;
            }

            var parentDirectory = Directory.GetParent(folderPath);
            if ( parentDirectory == null ) {
                Debug.WriteLine( $"[MoveVideosToDateFoldersAsync] Could not determine parent directory of {folderPath}" );
                return newVideos;
            }

            var filesInFolder = Directory.GetFiles( folderPath, "*", SearchOption.TopDirectoryOnly );

            var movedFilePaths = await Task.Run( () => {
                var paths = new List<string>();
                foreach ( var filePath in filesInFolder ) {
                    try {
                        var fileInfo = new FileInfo(filePath);
                        var fileNameWithoutArtists = ArtistService.GetFileNameWithoutArtist(fileInfo.Name);

                        // DB内に同名のファイルが存在するかチェック
                        if ( IsFileNameDuplicateInDatabase( fileInfo.Name, fileNameWithoutArtists, null ) ) {
                            Debug.WriteLine( $"[MoveVideosToDateFoldersAsync] Skipping duplicate file (found in DB): {filePath}" );
                            continue;
                        }

                        var lastModified = fileInfo.LastWriteTime;
                        var targetFolderName = lastModified.ToString("yyyyMM");
                        var targetDirectoryPath = Path.Combine(parentDirectory.FullName, targetFolderName);

                        // 移動先フォルダがなければ作成
                        if ( !Directory.Exists( targetDirectoryPath ) ) {
                            Directory.CreateDirectory( targetDirectoryPath );
                        }

                        var targetFilePath = Path.Combine(targetDirectoryPath, fileInfo.Name);

                        // 移動先に同名ファイルが存在する場合はスキップ
                        if ( File.Exists( targetFilePath ) ) {
                            Debug.WriteLine( $"[MoveVideosToDateFoldersAsync] Target file already exists, skipping: {targetFilePath}" );
                            continue;
                        }

                        // ファイルを移動
                        File.Move( filePath, targetFilePath );

                        paths.Add( targetFilePath );
                        Debug.WriteLine( $"[MoveVideosToDateFoldersAsync] Moved {filePath} to {targetFilePath}" );
                    } catch ( Exception ex ) {
                        Debug.WriteLine( $"[MoveVideosToDateFoldersAsync] Error processing file {filePath}: {ex.Message}" );
                        // 1ファイルのエラーで全体を止めない
                    }
                }
                return paths;
            } );

            // メインスレッド（UIスレッド）に戻ってからDBとコレクションを更新
            foreach ( var path in movedFilePaths ) {
                var newVideo = await AddVideoFromPathAsync( path );
                if ( newVideo != null ) {
                    newVideos.Add( newVideo );
                }
            }
            return newVideos;
        }

        /// <summary>
        /// 指定されたパスから動画やフォルダを追加する
        /// </summary>
        private async Task<VideoItem?> AddVideoFromPathAsync( string path ) {
            if ( Videos.Any( v => v.FilePath == path ) )
                return null; // 既に存在する場合はスキップ

            try {
                VideoItem videoItem;
                // パスがディレクトリかファイルかを確認
                if ( Directory.Exists( path ) ) {
                    var dirInfo = new DirectoryInfo(path);
                    videoItem = new VideoItem( 0, path, dirInfo.Name, 0, dirInfo.LastWriteTime, 0 );
                    await _databaseService.AddVideoAsync( videoItem );
                    Videos.Add( videoItem );
                } else if ( File.Exists( path ) ) {
                    var fileInfo = new FileInfo(path);

                    // StorageFileを取得して詳細プロパティを取得
                    var file = await StorageFile.GetFileFromPathAsync(path);
                    var props = await file.GetBasicPropertiesAsync();
                    var videoProps = await file.Properties.GetVideoPropertiesAsync();
                    var fileNameWithoutArtists = ArtistService.GetFileNameWithoutArtist(file.Name);

                    videoItem = new VideoItem
                    {
                        Id = 0,
                        FilePath = file.Path,
                        FileName = file.Name,
                        FileNameWithoutArtists = fileNameWithoutArtists,
                        Extension = fileInfo.Extension.ToLower(),
                        FileSize = (long)props.Size,
                        LastModified = props.DateModified.DateTime,
                        Duration = videoProps.Duration.TotalSeconds
                    };
                    await _databaseService.AddVideoAsync( videoItem );
                    Videos.Add( videoItem );
                    _ = Task.Run( () => LoadThumbnailBytesAsync( videoItem ) );
                } else {
                    return null;
                }
                return videoItem;
            } catch ( Exception ex ) {
                Debug.WriteLine( $"Error adding item from path: {path}. Error: {ex.Message}" );
                return null;
            }
        }
    }
}