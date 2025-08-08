using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;

namespace VideoManager3_WinUI {
    /// <summary>
    /// 動画関連のデータ操作とビジネスロジックを管理するサービスクラス
    /// </summary>
    public class VideoService {
        public ObservableCollection<VideoItem> Videos { get; } = new ObservableCollection<VideoItem>();

        private readonly DatabaseService _databaseService;
        private readonly TagService _tagService;
        private readonly ThumbnailService _thumbnailService;
        private readonly DispatcherQueue _dispatcherQueue;

        public VideoService( DatabaseService databaseService, TagService tagService, ThumbnailService thumbnailService, DispatcherQueue dispatcherQueue ) {
            _databaseService = databaseService;
            _tagService = tagService;
            _thumbnailService = thumbnailService;
            _dispatcherQueue = dispatcherQueue;
        }

        /// <summary>
        /// データベースから動画を非同期にロードし、UIを更新します。
        /// </summary>
        public async Task LoadVideosAsync() {
            Videos.Clear();
            var videosFromDb = await _databaseService.GetAllVideosAsync();
            var allTagsLookup = _tagService.GetAllTagsAsDictionary();

            foreach ( var video in videosFromDb ) {
                Videos.Add( video );
                var tagsForVideo = await _databaseService.GetTagsForVideoAsync(video);

                foreach ( var tagFromDb in tagsForVideo ) {
                    if ( allTagsLookup.TryGetValue( tagFromDb.Id, out var existingTag ) ) {
                        video.VideoTagItems.Add( existingTag );
                    }
                }

                _ = Task.Run( () => LoadThumbnailAsync( video ) );
            }
        }

        /// <summary>
        /// 指定されたフォルダから動画とサブフォルダを追加します。
        /// </summary>
        public async Task AddVideosFromFolderAsync() {
            var folderPicker = new FolderPicker
            {
                SuggestedStartLocation = PickerLocationId.VideosLibrary
            };
            folderPicker.FileTypeFilter.Add( "*" );

            if ( App.m_window == null )
                return;
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.m_window);
            WinRT.Interop.InitializeWithWindow.Initialize( folderPicker, hwnd );

            StorageFolder folder = await folderPicker.PickSingleFolderAsync();
            if ( folder != null ) {
                var items = await folder.GetItemsAsync();
                foreach ( var item in items ) {
                    if ( item.IsOfType( StorageItemTypes.Folder ) && item is StorageFolder subFolder ) {
                        if ( !Videos.Any( v => v.FilePath == subFolder.Path ) ) {
                            var basicProperties = await subFolder.GetBasicPropertiesAsync();
                            var videoItem = new VideoItem(0, subFolder.Path, subFolder.Name, 0, basicProperties.DateModified.DateTime, 0);
                            await _databaseService.AddVideoAsync( videoItem );
                            Videos.Add( videoItem );
                        }
                    } else if ( item.IsOfType( StorageItemTypes.File ) && item is StorageFile file && file.FileType.ToLower() == ".mp4" ) {
                        if ( !Videos.Any( v => v.FilePath == file.Path ) ) {
                            var props = await file.GetBasicPropertiesAsync();
                            var videoProps = await file.Properties.GetVideoPropertiesAsync();
                            var videoItem = new VideoItem(0, file.Path, file.Name, (long)props.Size, props.DateModified.DateTime, videoProps.Duration.TotalSeconds);
                            await _databaseService.AddVideoAsync( videoItem );
                            Videos.Add( videoItem );
                            _ = Task.Run( () => LoadThumbnailAsync( videoItem ) );
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 動画のサムネイルを非同期で読み込み、UIスレッドで設定します。
        /// </summary>
        private async Task LoadThumbnailAsync( VideoItem videoItem ) {
            var imageBytes = await _thumbnailService.GetThumbnailBytesAsync(videoItem.FilePath);

            if ( imageBytes != null && imageBytes.Length > 0 ) {
                _dispatcherQueue.TryEnqueue( async () =>
                {
                    try {
                        var bitmapImage = new BitmapImage();
                        using var stream = new InMemoryRandomAccessStream();
                        await stream.WriteAsync( imageBytes.AsBuffer() );
                        stream.Seek( 0 );
                        await bitmapImage.SetSourceAsync( stream );
                        videoItem.Thumbnail = bitmapImage;
                    }
                    catch ( Exception ex ) {
                        Debug.WriteLine( $"Failed to set thumbnail source for {videoItem.FileName}: {ex.Message}" );
                    }
                } );
            }
        }

        /// <summary>
        /// 選択されたファイル（動画またはフォルダ）を開きます。
        /// ・動画：再生を開始
        /// ・フォルダ：フォルダを開く
        /// </summary>
        public void OpenFile( VideoItem? videoItem ) {
            if ( videoItem != null ) {
                try {
                    Process.Start( new ProcessStartInfo( videoItem.FilePath ) { UseShellExecute = true } );
                }
                catch ( Exception ex ) {
                    Debug.WriteLine( $"Error opening file: {ex.Message}" );
                }
            }
        }
    }

}
