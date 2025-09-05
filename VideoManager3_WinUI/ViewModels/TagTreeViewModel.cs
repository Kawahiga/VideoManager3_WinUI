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
        private readonly DatabaseService _databaseService; // EditTagAsyncのために必要

        public ObservableCollection<TagItem> TagItems { get; } = new ObservableCollection<TagItem>();

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

        public TagTreeViewModel( TagService tagService, DatabaseService databaseService ) {
            _tagService = tagService;
            _databaseService = databaseService; // インスタンスを受け取る

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
        }

        /// <summary>
        /// タグに紐づく動画情報を取得する。
        /// </summary>
        public Task LoadTagVideos( ObservableCollection<VideoItem>? allVideos ) {
            if ( allVideos == null || !allVideos.Any() ) {
                return Task.CompletedTask;
            }

            // 最初に全タグの動画リストをクリア
            var allTags = GetTagsInOrder();
            foreach ( var tag in allTags ) {
                tag.TagVideoItem.Clear();
            }

            // すべてのタグのIDをキーとする辞書を取得
            var tagDictionary = allTags.ToDictionary( t => t.Id );

            // 「タグなし」タグを探す。
            var untaggedTag = TagItems.FirstOrDefault(t => t.Name == "タグなし");
            untaggedTag?.TagVideoItem.Clear();

            // 動画リストをループしてタグに割り当てる
            foreach ( var video in allVideos ) {
                if ( video.VideoTagItems == null || !video.VideoTagItems.Any() ) {
                    // タグがない動画を「タグなし」に追加
                    untaggedTag?.TagVideoItem.Add( video );
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
        /// タグに紐づく動画情報を取得する。
        /// </summary>
        public async Task LoadTagVideos( ObservableCollection<VideoItem>? videos, ObservableCollection<TagItem>? tags ) {
            if ( videos == null || tags == null ) {
                return;
            }

            var orderedAllTags = GetTagsInOrder();
            foreach ( var tag in orderedAllTags ) {
                tag.TagVideoItem.Clear();
            }
            foreach ( var video in videos ) {
                video.VideoTagItems?.Clear();
            }
            // 「タグなし」タグを探す。
            var untaggedTag = orderedAllTags.FirstOrDefault(t => t.Name == "タグなし");
            untaggedTag?.TagVideoItem.Clear();

            var allVideos = new List<VideoItem>();
            foreach ( var video in videos ) {
                allVideos.Add( video );
            }

            foreach ( var video in allVideos ) {
                var tagsForVideoFromDb = await _tagService.LoadVideoTagAsync( video );
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
            return;
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
                // 「OK」
                if ( !string.IsNullOrWhiteSpace( inputTextBox.Text ) && tag.Name != inputTextBox.Text ) {
                    tag.Name = inputTextBox.Text;
                    await _tagService.AddOrUpdateTagAsync( tag );
                }
            } else if ( result == ContentDialogResult.Secondary ) {
                // 「削除」
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

            //targetItem.VideoTagItems.Clear();
            var tmpTag = GetTagsInOrder();
            foreach ( var tag in tmpTag ) {
                // チェック状態に応じてDBとViewModelを更新
                if ( tag.IsChecked ) {
                    // タグが既に追加されていなければ追加
                    if ( !targetItem.VideoTagItems.Any( t => t.Id == tag.Id ) ) {
                        await _databaseService.AddTagToVideoAsync( targetItem, tag );
                        targetItem.VideoTagItems.Add( tag );
                        tag.TagVideoItem.Add( targetItem ); // タグ側の関連付けも更新
                    }
                } else {
                    // タグが既に存在すれば削除
                    var tagToRemove = targetItem.VideoTagItems.FirstOrDefault(t => t.Id == tag.Id);
                    if ( tagToRemove != null ) {
                        await _databaseService.RemoveTagFromVideoAsync( targetItem, tagToRemove );
                        targetItem.VideoTagItems.Remove( tagToRemove );
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