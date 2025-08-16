using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using static MediaToolkit.Model.Metadata;

namespace VideoManager3_WinUI {
    public enum VideoSortType {
        LastModifiedDescending, // 更新日時降順
        LastModifiedAscending,  // 更新日時昇順
        FileNameAscending,      // ファイル名昇順
        FileNameDescending,      // ファイル名降順
        LikeCountDescending, // いいね数降順
        LikeCountAscending,  // いいね数昇順
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

            if ( imageBytes != null && imageBytes.Length > 0 ) {
                videoItem.Thumbnail = imageBytes;
            }
        }

        /// <summary>
        /// 動画に紐づくタグを設定します。
        /// </summary>
        public async Task LoadVideoTagsAsync() {
            var allTagsLookup = _tagService.GetAllTagsAsDictionary();
            foreach ( var video in Videos ) {
                video.VideoTagItems.Clear();
                // 動画に紐づくタグを取得
                var tagsForVideo = await _databaseService.GetTagsForVideoAsync(video);
                foreach ( var tagFromDb in tagsForVideo ) {
                    if ( allTagsLookup.TryGetValue( tagFromDb.Id, out var existingTag ) ) {
                        video.VideoTagItems.Add( existingTag );
                    }
                }
            }
        }

        /// <summary>
        /// 指定されたフォルダから動画とサブフォルダを追加します。
        /// </summary>
        public async Task AddVideosFromFolderAsync() {
            try {
                if ( App.m_window == null )return;

                var folderPicker = new FolderPicker
                {
                    SuggestedStartLocation = PickerLocationId.VideosLibrary
                };
                folderPicker.FileTypeFilter.Add( "*" );

                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.m_window);
                WinRT.Interop.InitializeWithWindow.Initialize( folderPicker, hwnd );

                StorageFolder folder = await folderPicker.PickSingleFolderAsync();
                if ( folder != null ) {
                    var items = await folder.GetItemsAsync();
                    foreach ( var item in items ) {
                        await AddVideoFromPathAsync( item.Path );
                    }
                }
            } catch ( Exception ex ) {
                Debug.WriteLine( $"Error adding videos from folder: {ex.Message}" );
            }
        }

        /// <summary>
        /// 指定されたパスのリストから動画やフォルダを追加します。
        /// </summary>
        public async Task AddVideosFromPathsAsync( IEnumerable<string> paths ) {
            foreach ( var path in paths ) {
                await AddVideoFromPathAsync( path );
            }
        }

        /// <summary>
        /// 指定されたパスから動画やフォルダを追加する共通メソッド
        /// </summary>
        private async Task AddVideoFromPathAsync( string path ) {
            if ( Videos.Any( v => v.FilePath == path ) ) {
                return; // 既に存在する場合はスキップ
            }

            try {
                // パスがディレクトリかファイルかを確認
                if ( Directory.Exists( path ) ) {
                    var dirInfo = new DirectoryInfo(path);
                    var videoItem = new VideoItem(0, path, dirInfo.Name, 0, dirInfo.LastWriteTime, 0);
                    await _databaseService.AddVideoAsync( videoItem );
                    Videos.Add( videoItem );
                } else if ( File.Exists( path ) ) {
                    var fileInfo = new FileInfo(path);
                    if ( fileInfo.Extension.ToLower() == ".mp4" ) // .mp4ファイルのみを対象とする
                    {
                        // StorageFileを取得して詳細プロパティを取得
                        var file = await StorageFile.GetFileFromPathAsync(path);
                        var props = await file.GetBasicPropertiesAsync();
                        var videoProps = await file.Properties.GetVideoPropertiesAsync();
                        var videoItem = new VideoItem
                        {
                            Id = 0,
                            FilePath = file.Path,
                            FileName = file.Name,
                            Extension = fileInfo.Extension.ToLower(),
                            FileSize = (long)props.Size,
                            LastModified = props.DateModified.DateTime,
                            Duration = videoProps.Duration.TotalSeconds
                        };
                        await _databaseService.AddVideoAsync( videoItem );
                        Videos.Add( videoItem );
                        _ = Task.Run( () => LoadThumbnailBytesAsync( videoItem ) );
                    }
                }
            } catch ( Exception ex ) {
                Debug.WriteLine( $"Error adding item from path: {path}. Error: {ex.Message}" );
            }
        }

        /// <summary>
        /// 選択された動画を削除します。
        /// </summary>
        public async Task DeleteVideoAsync( VideoItem? videoItem ) {
            if ( videoItem == null || string.IsNullOrEmpty( videoItem.FilePath ) )
                return;
            try {
                // データベースから動画を削除
                await _databaseService.DeleteVideoAsync( videoItem );
                // UIから動画を削除
                Videos.Remove( videoItem );
                // ファイルシステムからも削除
                //var file = await StorageFile.GetFileFromPathAsync(videoItem.FilePath);
                //await file.DeleteAsync();
            } catch ( Exception ex ) {
                Debug.WriteLine( $"Error deleting video: {ex.Message}" );
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
    }
}