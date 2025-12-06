using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using VideoManager3_WinUI.Models;
using VideoManager3_WinUI.Services;

namespace VideoManager3_WinUI.ViewModels {
    public partial class TagTreeViewModel:ObservableObject {
        private readonly TagService _tagService;

        public ObservableCollection<TagItem> TagItems { get; } = new ObservableCollection<TagItem>();
        private TagItem? _allFilesTag;
        public ObservableCollection<TagItem> TagTreeItems { get; } = new();

        // TreeViewで選択されているタグを保持するためのプロパティ
        private TagItem? _selectedTag;
        public TagItem? SelectedTag {
            get => _selectedTag;
            set {
                if ( SetProperty( ref _selectedTag, value ) && !IsTagSetting ) {
                    SelectedTagChanged?.Invoke( value );
                }
            }
        }

        public IRelayCommand<TagItem> EditTagCommand { get; }

        public event Action<TagItem?>? SelectedTagChanged;

        public bool IsTagSetting = false;

        public TagTreeViewModel( TagService tagService ) {
            _tagService = tagService;

            EditTagCommand = new RelayCommand<TagItem>( async ( tag ) => await EditTagAsync( tag ) );
        }

        /// <summary>
        /// 全てのタグをロードする
        /// </summary>
        public async Task LoadTagsAsync() {
            // TagServiceからルートタグを取得してUIコレクションを構築
            var rootTags = await _tagService.LoadTagsAsync();
            TagItems.Clear();
            foreach ( var tag in rootTags ) {
                TagItems.Add( tag );
            }

            _allFilesTag = TagItems.FirstOrDefault();

            TagTreeItems.Clear();
            if ( _allFilesTag != null ) {
                foreach ( var child in _allFilesTag.Children ) {
                    TagTreeItems.Add( child );
                }
            }
        }

        /// <summary>
        /// タグの編集ダイアログを表示
        /// </summary>
        public async Task EditTagAsync( TagItem? tag ) {
            if ( App.MainWindow == null || tag == null )
                return;

            var inputTextBox = new TextBox
            {
                AcceptsReturn = false,
                Height = 32,
                Text = tag.Name,
                SelectionStart = tag.Name.Length
            };

            var dialog = new ContentDialog
            {
                Title = "タグの編集",
                Content = inputTextBox,
                PrimaryButtonText = "OK",
                SecondaryButtonText = "削除",
                CloseButtonText = "キャンセル",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = App.MainWindow.Content.XamlRoot
            };

            var result = await dialog.ShowAsync();

            if ( result == ContentDialogResult.Primary ) {
                // 「OK」ボタンが押された
                if ( !string.IsNullOrWhiteSpace( inputTextBox.Text ) && tag.Name != inputTextBox.Text ) {
                    tag.Name = inputTextBox.Text;
                    await _tagService.AddOrUpdateTagAsync( tag );
                }
            } else if ( result == ContentDialogResult.Secondary ) {
                // 「削除」ボタンが押された
                RemoveTagFromUI( TagItems, tag );
                foreach ( var video in tag.TagVideoItem ) {
                    video.VideoTagItems.Remove( tag );
                }
                await _tagService.DeleteTagAsync( tag );
            }
        }
        // タグリストから再帰的にタグを削除する
        private bool RemoveTagFromUI( ObservableCollection<TagItem> tags, TagItem tagToRemove ) {
            if ( tags.Remove( tagToRemove ) ) {
                return true;
            }

            foreach ( var tag in tags ) {
                if ( RemoveTagFromUI( tag.Children, tagToRemove ) ) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// アプリを終了する際にタグ情報を保存する
        /// </summary>
        public async Task SaveTagsInClose() {
            await SaveTagsRecursively( TagItems );
        }
        private async Task SaveTagsRecursively( ObservableCollection<TagItem> tags ) {
            try {
                foreach ( var tag in tags ) {
                    if ( tag.IsModified )
                        await _tagService.AddOrUpdateTagAsync( tag );
                    if ( tag.Children.Any() )
                        await SaveTagsRecursively( tag.Children );
                }
            } catch ( Exception ex ) {
                System.Diagnostics.Debug.WriteLine( $"Error saving tags: {ex.Message}" );
            }
        }

        /// <summary>
        /// ファイルに対するタグ設定コマンドの前準備
        /// </summary>
        public void PrepareTagsForEditing( VideoItem? selectedItem ) {
            if ( selectedItem == null ) {
                return;
            }

            // 現在選択されている動画が持っているタグIDのリストを作成
            var checkedTagIds = new HashSet<int>(selectedItem.VideoTagItems.Select(t => t.Id));
            // 全てのタグを表示順で取得
            var allTags = GetTagsInOrder();

            foreach ( var tag in allTags ) {
                if ( !tag.IsGroup ) {
                    tag.IsChecked = checkedTagIds.Contains( tag.Id );
                    tag.IsEditing = true;
                } else {
                    // グループは設定不可
                    tag.IsEditing = false;
                }
            }
        }

        /// <summary>
        /// ファイルに対するタグ設定コマンド
        /// </summary>
        public async Task UpdateVideoTagSelection( VideoItem? targetItem ) {
            if ( targetItem == null ) {
                return;
            }

            var tmpTag = GetTagsInOrder();
            foreach ( var tag in tmpTag ) {
                // チェック状態に応じてDBとViewModelを更新
                if ( tag.IsChecked ) {
                    // ビデオが既にタグを持っていなければ追加
                    if ( !targetItem.VideoTagItems.Any( t => t.Id == tag.Id ) ) {
                        await _tagService.AddTagToVideoAsync( targetItem, tag );
                        targetItem.VideoTagItems.Add( tag );
                        tag.TagVideoItem.Add( targetItem ); // タグ側の関連付けも更新
                    }
                } else {
                    // タグが既に存在すれば削除
                    var tagToRemove = targetItem.VideoTagItems.FirstOrDefault(t => t.Id == tag.Id);
                    if ( tagToRemove != null ) {
                        await _tagService.DeleteTagToVideoAsync( targetItem, tag );
                        targetItem.VideoTagItems.Remove( tag );
                        tag.TagVideoItem.Remove( targetItem ); // タグ側の関連付けも更新
                    }
                }
                // タグの編集モードを解除
                tag.IsEditing = false;
            }
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
    }
}