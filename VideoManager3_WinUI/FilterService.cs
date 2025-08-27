using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace VideoManager3_WinUI {
    class FilterService {
        public ObservableCollection<FilterItem> Filters { get; } = new ObservableCollection<FilterItem>();

        public void SetTagFilter( TagItem? tag ) {
            // 「全てのファイル」またはnullの場合は、タグフィルターを解除
            UpdateFilter( FilterType.Tag, tag, tag?.Name, tag == null || tag.Name == "全てのファイル" );
        }

        public void SetArtistFilter( ArtistItem? artist ) {
            // artistがnullの場合は、アーティストフィルターを解除
            UpdateFilter( FilterType.Artist, artist, artist?.Name, artist == null );
        }

        public void SetSearchTextFilter( string? searchText ) {
            // searchTextが空の場合は、検索フィルターを解除
            UpdateFilter( FilterType.SearchText, searchText, searchText, string.IsNullOrWhiteSpace( searchText ) );
        }

        /// <summary>
        /// フィルターの更新（追加または削除）を行う
        /// </summary>
        private void UpdateFilter( FilterType type, object? value, string? label, bool shouldRemove ) {
            var existingFilter = Filters.FirstOrDefault(f => f.Type == type);
            if ( existingFilter != null ) {
                Filters.Remove( existingFilter );
            }

            if ( !shouldRemove && value != null && label != null ) {
                Filters.Add( new FilterItem( type, value, label, ( item, videos ) => { } ) );
            }
        }

        /// <summary>
        /// 動画リストにフィルターを適用する
        /// </summary>
        public IEnumerable<VideoItem> ApplyFilters( IEnumerable<VideoItem> videos ) {
            var activeFilters = Filters.Where(f => f.IsActive).ToList();
            if ( !activeFilters.Any() ) {
                return videos;
            }
            return videos.Where( video => activeFilters.All( filter => IsVideoMatch( video, filter ) ) );
        }

        private bool IsVideoMatch( VideoItem video, FilterItem filter ) {
            switch ( filter.Type ) {
                case FilterType.Tag:
                return filter.Value is TagItem tag && IsMatchByTag( video, tag );
                case FilterType.Artist:
                return filter.Value is ArtistItem artist && IsMatchByArtist( video, artist );
                case FilterType.SearchText:
                return filter.Value is string searchText && IsMatchBySearchText( video, searchText );
                default:
                return true;
            }
        }

        /// <summary>
        /// 動画が指定されたタグにマッチするかどうかを判定します。 
        /// </summary>
        private bool IsMatchByTag( VideoItem video, TagItem selectedTag ) {
            if ( selectedTag.Name.Equals( "タグなし" ) ) {
                return !video.VideoTagItems.Any();
            }
            var tagIds = new HashSet<int>(selectedTag.GetAllDescendantIds());
            return video.VideoTagItems.Any( videoTag => tagIds.Contains( videoTag.Id ) );
        }

        /// <summary>
        /// 動画が指定されたアーティストにマッチするかどうかを判定します。
        /// </summary>
        private bool IsMatchByArtist( VideoItem video, ArtistItem selectedArtist ) {
            return video.ArtistsInVideo.Any( a => a.Id == selectedArtist.Id );
        }

        /// <summary>
        /// 動画のファイル名が検索テキストにマッチするかどうかを判定します。
        /// </summary>
        private bool IsMatchBySearchText( VideoItem video, string searchText ) {
            if ( string.IsNullOrEmpty( video.FileName ) )
                return false;
            var searchKeywords = searchText.ToLower().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var fileNameLower = video.FileName.ToLower();
            return searchKeywords.All( keyword => fileNameLower.Contains( keyword ) );
        }
    }
}
