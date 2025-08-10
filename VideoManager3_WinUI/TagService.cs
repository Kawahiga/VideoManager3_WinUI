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
            }
            catch ( Exception ex ) {
                System.Diagnostics.Debug.WriteLine( $"Error loading tags from database: {ex.Message}" );
            }
        }

        /// <summary>
        /// タグに紐づく動画を非同期にロードします。
        /// </summary>
        /// <returns></returns>
        public async Task LoadTagVideosAsync(  ) {

        }

        /// <summary>
        /// タグをデータベースに追加または更新し、UIをリフレッシュします。
        /// </summary>
        public async Task AddOrUpdateTagAsync( TagItem tag ) {
            await _databaseService.AddOrUpdateTagAsync( tag );
            await LoadTagsAsync(); // タグツリーを再読み込みしてUIを更新
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
        public Dictionary<int, TagItem> GetAllTagsAsDictionary() {
            return _allTags.ToDictionary( t => t.Id );
        }

        protected virtual void OnPropertyChanged( string propertyName ) {
            PropertyChanged?.Invoke( this, new PropertyChangedEventArgs( propertyName ) );
        }
    }
}
