using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using VideoManager3_WinUI.Models;
using static MediaToolkit.Model.Metadata;

namespace VideoManager3_WinUI.Services {
    /// <summary>
    /// タグ関連のデータ操作とビジネスロジックを管理するサービスクラス
    /// </summary>
    public class TagService {
        private readonly DatabaseService _databaseService;

        public TagService( DatabaseService databaseService ) {
            _databaseService = databaseService;
        }

        /// <summary>
        /// データベースから非同期にタグをロードし、階層構造を構築します。
        /// </summary>
        public async Task<List<TagItem>> LoadTagsAsync() {
            try {
                var _allTags = await _databaseService.GetTagsAsync();
                var tagDict = _allTags.ToDictionary(t => t.Id);

                var rootTags = new List<TagItem>();

                // 1. タグの階層を構築
                foreach ( var tag in _allTags ) {
                    if ( tag.ParentId.HasValue && tag.ParentId != 0
                        && tagDict.TryGetValue( tag.ParentId.Value, out var parentTag ) ) {
                        parentTag.Children.Add( tag );
                    } else {
                        rootTags.Add( tag );
                    }
                }

                // 2. 子タグとルートタグを順序でソート
                foreach ( var tag in _allTags.Where( t => t.Children.Any() ) ) {
                    var sortedChildren = tag.Children.OrderBy(c => c.OrderInGroup).ToList();
                    tag.Children.Clear();
                    sortedChildren.ForEach( tag.Children.Add );
                }
                var sortedRootTags = rootTags.OrderBy(t => t.OrderInGroup).ToList();

                return sortedRootTags;
            } catch ( Exception ex ) {
                System.Diagnostics.Debug.WriteLine( $"Error loading tags from database: {ex.Message}" );
                return new List<TagItem>();
            }
        }

        /// <summary>
        /// データベースから非同期に動画とタグの紐づけ情報をロードします。
        /// </summary>
        public async Task LoadVideoTagAsync( List<VideoItem> videos, List<TagItem> orderedAllTags ) {
            var allTagsLookup = orderedAllTags.ToDictionary(t => t.Id);

            foreach ( var tag in orderedAllTags ) {
                tag.TagVideoItem.Clear();
            }
            // 「タグなし」タグを探す。
            var untaggedTag = orderedAllTags.FirstOrDefault(t => t.Name == "タグなし");
            untaggedTag?.TagVideoItem.Clear();

            foreach ( var video in videos ) {
                video.VideoTagItems.Clear();
                var tagsForVideoFromDb = await _databaseService.GetTagsForVideoAsync(video);
                if ( tagsForVideoFromDb.Count == 0 ) {
                    // タグが1つも設定されていない場合、「タグなし」に設定
                    untaggedTag?.TagVideoItem.Add( video );
                    continue;
                }

                var tagsForVideoIds = new HashSet<int>(tagsForVideoFromDb.Select(t => t.Id));
                // orderedAllTags の順序を維持しつつ、このビデオに紐づくタグのみをフィルタリングして追加
                foreach ( var tag in orderedAllTags ) {
                    if ( tagsForVideoIds.Contains( tag.Id ) ) {
                        video.VideoTagItems.Add( tag );
                        tag.TagVideoItem.Add( video );
                    }
                }
            }
        }


        /// <summary>
        /// タグをデータベースに追加または更新します。
        /// </summary>
        public async Task AddOrUpdateTagAsync( TagItem tag ) {
            await _databaseService.AddOrUpdateTagAsync( tag );
        }

        /// <summary>
        /// タグをデータベースから削除します。
        /// </summary>
        public async Task DeleteTagAsync( TagItem tag ) {
            await _databaseService.DeleteTagAsync( tag );
        }

        ///// <summary>
        ///// 選択中の動画が持つタグ情報に基づき、全タグのチェック状態を更新します。
        ///// </summary>
        ///// <param name="checkedVideoTags">動画に紐づくタグのコレクション</param>
        //public void UpdateTagCheckedState( IEnumerable<TagItem> checkedVideoTags ) {
        //    var checkedTagIds = new HashSet<int>(checkedVideoTags.Select(t => t.Id));

        //    foreach ( var tag in _allTags ) {
        //        tag.IsChecked = checkedTagIds.Contains( tag.Id );
        //    }
        //}

        ///// <summary>
        ///// すべてのタグをツリーの表示順でフラットなリストとして取得します。
        ///// </summary>
        ///// <returns>順序付けされたタグのリスト</returns>
        //public List<TagItem> GetTagsInOrder() {
        //    var orderedTags = new List<TagItem>();
        //    void Traverse( IEnumerable<TagItem> tags ) {
        //        foreach ( var tag in tags ) {
        //            orderedTags.Add( tag );
        //            Traverse( tag.Children );
        //        }
        //    }
        //    Traverse( TagItems );
        //    return orderedTags;
        //}
    }
}
