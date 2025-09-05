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
        public async Task<List<TagItem>> LoadVideoTagAsync( VideoItem video ) {
            return await _databaseService.GetTagsForVideoAsync( video );
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
    }
}
