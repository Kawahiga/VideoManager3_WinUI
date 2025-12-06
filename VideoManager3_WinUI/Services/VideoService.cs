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
        private static readonly string[] SupportedVideoExtensions = { ".mp4", ".mov", ".wmv", ".avi", ".mkv", ".mpeg", ".mpg", ".flv", ".webm", ".fid", ".dcv", ".m4v" };

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
        /// フォルダの場合は、先頭の動画ファイルからサムネイル画像を生成します。
        /// </summary>
        private async Task LoadThumbnailBytesAsync( VideoItem videoItem ) {
            if ( videoItem == null || string.IsNullOrEmpty( videoItem.FilePath ) )
                return;

            string? thumbnailSourcePath = videoItem.FilePath;

            // パスがディレクトリかどうかを確認
            if ( Directory.Exists( videoItem.FilePath ) ) {
                try {
                    // サポートされている拡張子を持つ最初のファイルを検索
                    thumbnailSourcePath = Directory.EnumerateFiles( videoItem.FilePath, "*.*", SearchOption.TopDirectoryOnly )
                                                   .FirstOrDefault( f => SupportedVideoExtensions.Contains( Path.GetExtension( f ).ToLowerInvariant() ) );
                } catch ( Exception ex ) {
                    Debug.WriteLine( $"Error searching for video file in directory {videoItem.FilePath}: {ex.Message}" );
                    thumbnailSourcePath = null; // エラーが発生した場合はサムネイル生成を中止
                }
            }

            if ( string.IsNullOrEmpty( thumbnailSourcePath ) ) {
                return; // サムネイルのソースが見つからない場合は処理を終了
            }

            var imageBytes = await _thumbnailService.GetThumbnailBytesAsync( thumbnailSourcePath );

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
        /// 選択された動画またはフォルダを削除します。
        /// ファイル/フォルダを先に削除し、成功した場合にDBとセッションデータを削除します。
        /// </summary>
        /// <returns>削除に成功した場合は true、失敗した場合は false。</returns>
        public async Task<bool> DeleteVideoAsync( VideoItem? videoItem ) {
            if ( videoItem == null || string.IsNullOrEmpty( videoItem.FilePath ) )
                return false;

            // 1. ファイルシステムから削除を試みる
            try {
                if ( Directory.Exists( videoItem.FilePath ) ) {
                    Directory.Delete( videoItem.FilePath, true ); // Recursive delete
                } else if ( File.Exists( videoItem.FilePath ) ) {
                    File.Delete( videoItem.FilePath );
                } else {
                    // ファイル/フォルダが既に存在しない場合は、成功とみなし、DBからの削除処理に進む
                    Debug.WriteLine( $"Path not found, but proceeding with DB deletion: {videoItem.FilePath}" );
                }
            } catch ( Exception ex ) {
                // ファイルまたはディレクトリの削除エラー（アクセス拒否など）
                Debug.WriteLine( $"Error deleting file or directory: {videoItem.FilePath}. Error: {ex.Message}" );
                // 削除に失敗したため、処理を中断
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
        /// ・フォルダ：フォルダ内に動画があれば先頭の動画を再生、なければフォルダを開く
        /// </summary>
        public void OpenFile( VideoItem? videoItem ) {
            if ( videoItem == null || string.IsNullOrEmpty( videoItem.FilePath ) )
                return;

            string? pathToOpen;

            if ( Directory.Exists( videoItem.FilePath ) ) {
                // フォルダの場合、中の最初の動画ファイルを探す
                try {
                    var firstVideo = Directory.EnumerateFiles( videoItem.FilePath, "*.*", SearchOption.TopDirectoryOnly )
                                              .FirstOrDefault( f => SupportedVideoExtensions.Contains( Path.GetExtension( f ).ToLowerInvariant() ) );

                    // 動画ファイルが見つかればそれを、なければフォルダのパスをそのまま使う
                    pathToOpen = firstVideo ?? videoItem.FilePath;

                } catch ( Exception ex ) {
                    Debug.WriteLine( $"Error reading directory {videoItem.FilePath}: {ex.Message}" );
                    // フォルダの読み取りでエラーが発生した場合は、とりあえずフォルダを開く試みをする
                    pathToOpen = videoItem.FilePath;
                }
            } else {
                // ファイルの場合
                pathToOpen = videoItem.FilePath;
            }

            if ( string.IsNullOrEmpty( pathToOpen ) || !(File.Exists( pathToOpen ) || Directory.Exists( pathToOpen )) ) {
                Debug.WriteLine( $"No valid file or directory to open for: {videoItem.FilePath}" );
                return;
            }

            try {
                Process.Start( new ProcessStartInfo( pathToOpen ) { UseShellExecute = true } );
                videoItem.ViewCount++; // 再生数を1増やす
                // DBに更新を反映
                _ = Task.Run( async () => await _databaseService.UpdateVideoAsync( videoItem ) );
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
        /// 指定された名前（拡張子を除く）に一致する重複したVideoItemをDB内から検索します。
        /// ファイルとフォルダを区別せず、拡張子を除いた名前で比較します。
        /// </summary>
        /// <param name="fileName">チェックする完全なファイル名またはフォルダ名。</param>
        /// <param name="fileNameWithoutArtists">チェックするアーティスト名を除いたファイル名またはフォルダ名。</param>
        /// <param name="excludeVideo">チェック対象から除外するビデオ（オプション）。</param>
        /// <returns>重複するVideoItemのシーケンス。</returns>
        private IEnumerable<VideoItem> GetDuplicateVideos( string fileName, string fileNameWithoutArtists, VideoItem? excludeVideo ) {
            var idToExclude = excludeVideo?.Id ?? -1;

            // ファイルが動画拡張子を持つ場合のみ、その拡張子を安全に除去するヘルパー関数
            string GetNameWithoutVideoExtension( string? name ) {
                // nameがnullまたは空の場合は、空文字列を返す
                if ( string.IsNullOrEmpty( name ) ) {
                    return string.Empty;
                }

                string ext = Path.GetExtension(name);
                // サポートされている動画拡張子リストに含まれているか、大文字小文字を無視してチェック
                if ( !string.IsNullOrEmpty( ext ) && SupportedVideoExtensions.Contains( ext, StringComparer.OrdinalIgnoreCase ) ) {
                    // nameから拡張子の部分を削除して返す
                    return name.Substring( 0, name.Length - ext.Length );
                }
                // 動画拡張子でなければ、または拡張子がなければ、元の名前を返す
                return name;
            }

            // チェック対象の名前から動画拡張子を除去
            var fileNameToCheck = GetNameWithoutVideoExtension(fileName);
            var fileNameWithoutArtistsToCheck = string.IsNullOrEmpty(fileNameWithoutArtists) ? string.Empty : GetNameWithoutVideoExtension(fileNameWithoutArtists);

            // 大文字小文字を区別せずに比較
            return Videos.Where( v => {
                if ( v.Id == idToExclude ) {
                    return false;
                }

                // DB内のアイテム名から動画拡張子を除去
                var dbItemName = GetNameWithoutVideoExtension(v.FileName);
                var dbItemNameWithoutArtists = string.IsNullOrEmpty( v.FileNameWithoutArtists ) ? string.Empty : GetNameWithoutVideoExtension(v.FileNameWithoutArtists);

                // 1. 拡張子を除いた名前が一致するか
                if ( string.Equals( dbItemName, fileNameToCheck, StringComparison.OrdinalIgnoreCase ) ) {
                    return true;
                }

                // 2. 拡張子とアーティスト名を除いた名前が一致するか
                if ( !string.IsNullOrEmpty( fileNameWithoutArtistsToCheck ) &&
                     !string.IsNullOrEmpty( dbItemNameWithoutArtists ) &&
                     string.Equals( dbItemNameWithoutArtists, fileNameWithoutArtistsToCheck, StringComparison.OrdinalIgnoreCase ) ) {
                    return true;
                }

                return false;
            } );
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
        /// 動画ファイルまたはフォルダの名前を変更します。
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
            if ( File.Exists( newPath ) || Directory.Exists( newPath ) || IsFileNameDuplicateInDatabase( newFileName, newFileNameWithoutArtists, videoItem ) ) {
                return RenameResult.AlreadyExists;
            }


            try {
                if ( Directory.Exists( oldPath ) ) {
                    Directory.Move( oldPath, newPath );
                } else if ( File.Exists( oldPath ) ) {
                    File.Move( oldPath, newPath );
                } else {
                    Debug.WriteLine( $"Source path not found for renaming: {oldPath}" );
                    return RenameResult.UnknownError;
                }

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
        /// 指定されたフォルダ内のファイルやサブフォルダと重複する名前を持つビデオをDB内から検索します。
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
                var fileSystemEntries = Directory.GetFileSystemEntries(folderPath, "*", SearchOption.TopDirectoryOnly);

                foreach ( var entryPath in fileSystemEntries ) {
                    var itemName = Path.GetFileName(entryPath);
                    var itemNameWithoutArtists = ArtistService.GetFileNameWithoutArtist(itemName);

                    // DB内のビデオと照合
                    var matches = GetDuplicateVideos(itemName, itemNameWithoutArtists, null);

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
        /// 指定されたフォルダ内のファイルやサブフォルダを、それぞれの更新日付に合わせたフォルダに移動します。
        /// DB内に同名のアイテムが存在する場合はスキップします。
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

            var fileSystemEntries = Directory.GetFileSystemEntries( folderPath, "*", SearchOption.TopDirectoryOnly );

            var movedItemPaths = await Task.Run( () => {
                var paths = new List<string>();
                foreach ( var entryPath in fileSystemEntries ) {
                    try {
                        string itemName;
                        string itemNameWithoutArtists;
                        DateTime lastModified;
                        bool isDirectory = Directory.Exists(entryPath);

                        if ( isDirectory ) {
                            var dirInfo = new DirectoryInfo(entryPath);
                            itemName = dirInfo.Name;
                            lastModified = dirInfo.LastWriteTime;
                        } else { // is File
                            var fileInfo = new FileInfo(entryPath);
                            itemName = fileInfo.Name;
                            lastModified = fileInfo.LastWriteTime;
                        }

                        itemNameWithoutArtists = ArtistService.GetFileNameWithoutArtist(itemName);

                        // DB内に同名のアイテムが存在するかチェック
                        if ( IsFileNameDuplicateInDatabase( itemName, itemNameWithoutArtists, null ) ) {
                            Debug.WriteLine( $"[MoveVideosToDateFoldersAsync] Skipping duplicate item (found in DB): {entryPath}" );
                            continue;
                        }

                        var targetFolderName = lastModified.ToString("yyyyMM");
                        var targetDirectoryPath = Path.Combine(parentDirectory.FullName, targetFolderName);

                        // 移動先フォルダがなければ作成
                        if ( !Directory.Exists( targetDirectoryPath ) ) {
                            Directory.CreateDirectory( targetDirectoryPath );
                        }

                        var targetPath = Path.Combine(targetDirectoryPath, itemName);

                        // 移動先に同名アイテムが存在する場合はスキップ
                        if ( Directory.Exists( targetPath ) || File.Exists( targetPath ) ) {
                            Debug.WriteLine( $"[MoveVideosToDateFoldersAsync] Target item already exists, skipping: {targetPath}" );
                            continue;
                        }

                        // アイテムを移動
                        if ( isDirectory ) {
                            Directory.Move( entryPath, targetPath );
                        } else {
                            File.Move( entryPath, targetPath );
                        }

                        paths.Add( targetPath );
                        Debug.WriteLine( $"[MoveVideosToDateFoldersAsync] Moved {entryPath} to {targetPath}" );
                    } catch ( Exception ex ) {
                        Debug.WriteLine( $"[MoveVideosToDateFoldersAsync] Error processing item {entryPath}: {ex.Message}" );
                        // 1アイテムのエラーで全体を止めない
                    }
                }
                return paths;
            } );

            // メインスレッド（UIスレッド）に戻ってからDBとコレクションを更新
            foreach ( var path in movedItemPaths ) {
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
                    var fileNameWithoutArtists = ArtistService.GetFileNameWithoutArtist(dirInfo.Name);
                    videoItem = new VideoItem
                    {
                        Id = 0,
                        FilePath = path,
                        FileName = dirInfo.Name,
                        FileNameWithoutArtists = fileNameWithoutArtists,
                        Extension = "",
                        FileSize = 0,       // 将来的にフォルダ内のファイルサイズ合計を計算する
                        LastModified = dirInfo.LastWriteTime,
                        Duration = 0        // 将来的にフォルダ内の動画の合計時間を計算する
                    };
                } else if ( File.Exists( path ) ) {
                    var fileExtension = Path.GetExtension(path).ToLower();
                    if ( !SupportedVideoExtensions.Contains( fileExtension ) ) {
                        return null; // サポートされていない拡張子
                    }
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
                } else {
                    return null;
                }
                await _databaseService.AddVideoAsync( videoItem );
                Videos.Add( videoItem );
                _ = Task.Run( () => LoadThumbnailBytesAsync( videoItem ) );

                return videoItem;
            } catch ( Exception ex ) {
                Debug.WriteLine( $"Error adding item from path: {path}. Error: {ex.Message}" );
                return null;
            }
        }
    }
}