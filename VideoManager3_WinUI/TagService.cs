using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

namespace VideoManager3_WinUI {
    /// <summary>
    /// タグ関連のデータ操作とビジネスロジックを管理するサービスクラス
    /// </summary>
    public class TagService:INotifyPropertyChanged {
        private readonly DatabaseService _databaseService;

        /// <summary>
        /// UIにバインドされる階層化されたタグのコレクション
        /// </summary>
        public ObservableCollection<TagItem> TagItems { get; } = new ObservableCollection<TagItem>();

        /// <summary>
        /// すべてのタグをフラットなリストで保持
        /// </summary>
        private List<TagItem> _allTags = new List<TagItem>();

        public event PropertyChangedEventHandler? PropertyChanged;


        public TagService( DatabaseService databaseService ) {
            _databaseService = databaseService;
        }

        /// <summary>
        /// データベースから非同期にタグをロードし、階層構造を構築してUIを更新します。
        /// </summary>
        public async Task LoadTagsAsync() {
            try {
                _allTags = await _databaseService.GetTagsAsync();
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

                // 3. UIのタグツリーを更新
                TagItems.Clear();
                sortedRootTags.ForEach( TagItems.Add );
                OnPropertyChanged( nameof( TagItems ) );
            } catch ( Exception ex ) {
                System.Diagnostics.Debug.WriteLine( $"Error loading tags from database: {ex.Message}" );
            }
        }

        /// <summary>
        /// タグに紐づく動画情報を取得しする。
        /// </summary>
        public Task LoadTagVideos( VideoService videoService ) {
            // 最初に全タグの動画リストをクリア
            foreach ( var tag in _allTags ) {
                tag.TagVideoItem.Clear();
            }

            var allVideos = videoService.Videos;
            if ( allVideos == null || !allVideos.Any() ) {
                return Task.CompletedTask;
            }

            var tagDictionary = GetAllTagsAsDictionary();

            // 「タグなし」タグを探す。まずDB由来の全タグリストから探す
            var untaggedTag = _allTags.FirstOrDefault(t => t.Name == "タグなし");

            // DBに「タグなし」タグがなければ、UI表示用に一時的なものを作成する
            if ( untaggedTag == null ) {
                // 念のため、UI上の既存の揮発的な「タグなし」タグを探す
                untaggedTag = TagItems.FirstOrDefault( t => t.Name == "タグなし" && t.Id == -1 );
                if ( untaggedTag == null ) {
                    untaggedTag = new TagItem { Id = -1, Name = "タグなし" }; // 一時的なID
                    TagItems.Add( untaggedTag ); // UIのルートに表示するためにコレクションに追加
                }
            }

            untaggedTag.TagVideoItem.Clear();

            // 動画リストをループしてタグに割り当てる
            foreach ( var video in allVideos ) {
                if ( video.VideoTagItems == null || !video.VideoTagItems.Any() ) {
                    // タグがない動画を「タグなし」に追加
                    untaggedTag.TagVideoItem.Add( video );
                } else {
                    // 既存のタグに動画を割り当てる
                    foreach ( var videoTag in video.VideoTagItems ) {
                        if ( tagDictionary.TryGetValue( videoTag.Id, out var targetTag ) ) {
                            targetTag.TagVideoItem.Add( video );
                        }
                    }
                }
            }
            return Task.CompletedTask;
        }

        /// <summary>
        /// タグをデータベースに追加または更新します。
        /// </summary>
        public async Task AddOrUpdateTagAsync( TagItem tag ) {
            await _databaseService.AddOrUpdateTagAsync( tag );
            _allTags.Add( tag );
        }

        /// <summary>
        /// タグをデータベースから削除します。
        /// </summary>
        public async Task DeleteTagAsync( TagItem tag ) {
            await _databaseService.DeleteTagAsync( tag );
            _allTags.Remove( tag );
            RemoveTagFromHierarchy( TagItems, tag );
            OnPropertyChanged( nameof( TagItems ) );
        }

        // UI上のタグ階層からタグを再帰的に削除
        private bool RemoveTagFromHierarchy( ObservableCollection<TagItem> tags, TagItem tagToRemove ) {
            if ( tags.Remove( tagToRemove ) ) {
                return true;
            }

            foreach ( var tag in tags ) {
                if ( RemoveTagFromHierarchy( tag.Children, tagToRemove ) ) {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 選択中の動画が持つタグ情報に基づき、全タグのチェック状態を更新します。
        /// </summary>
        /// <param name="checkedVideoTags">動画に紐づくタグのコレクション</param>
        public void UpdateTagCheckedState( IEnumerable<TagItem> checkedVideoTags ) {
            var checkedTagIds = new HashSet<int>(checkedVideoTags.Select(t => t.Id));

            foreach ( var tag in _allTags ) {
                tag.IsChecked = checkedTagIds.Contains( tag.Id );
            }
        }

        /// <summary>
        /// すべてのタグをIDをキーとする辞書として取得します。
        /// </summary>
        /// <returns>タグの辞書</returns>
        private Dictionary<int, TagItem> GetAllTagsAsDictionary() {
            return _allTags.ToDictionary( t => t.Id );
        }

        /// <summary>
        /// すべてのタグをツリーの表示順でフラットなリストとして取得します。
        /// </summary>
        /// <returns>順序付けされたタグのリスト</returns>
        public List<TagItem> GetTagsInOrder() {
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

        protected virtual void OnPropertyChanged( string propertyName ) {
            PropertyChanged?.Invoke( this, new PropertyChangedEventArgs( propertyName ) );
        }
    }
}
