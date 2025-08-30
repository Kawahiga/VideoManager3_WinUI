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
    //    private readonly TagService _tagService;
    //    private readonly DatabaseService _databaseService; // EditTagAsyncのために必要

    //    public ObservableCollection<TagItem> TagItems { get; } = new ObservableCollection<TagItem>();

    //    [ObservableProperty]
    //    private TagItem? _selectedTag;

    //    public IRelayCommand<TagItem> EditTagCommand { get; }

    //    public event Action<TagItem?>? SelectedTagChanged;

    //    public TagTreeViewModel( TagService tagService, DatabaseService databaseService ) {
    //        _tagService = tagService;
    //        _databaseService = databaseService; // インスタンスを受け取る

    //        EditTagCommand = new RelayCommand<TagItem>( async ( tag ) => await EditTagAsync( tag ) );

    //        // SelectedTagプロパティの変更を監視
    //        PropertyChanged += ( sender, e ) => {
    //            if ( e.PropertyName == nameof( SelectedTag ) ) {
    //                SelectedTagChanged?.Invoke( SelectedTag );
    //            }
    //        };
    //    }

    //    public async Task LoadTagsAsync() {
    //        // TagServiceからルートタグを取得してUIコレクションを構築
    //        var rootTags = await _tagService.GetRootTagsAsync();
    //        TagItems.Clear();
    //        foreach ( var tag in rootTags ) {
    //            TagItems.Add( tag );
    //        }
    //    }

    //    // MainViewModelから移管
    //    public async Task EditTagAsync( TagItem? tag ) {
    //        if ( App.MainWindow == null || tag == null )
    //            return;

    //        var inputTextBox = new TextBox
    //        {
    //            AcceptsReturn = false,
    //            Height = 32,
    //            Text = tag.Name,
    //            SelectionStart = tag.Name.Length
    //        };

    //        var dialog = new ContentDialog
    //        {
    //            Title = "タグの編集",
    //            Content = inputTextBox,
    //            PrimaryButtonText = "OK",
    //            SecondaryButtonText = "削除",
    //            CloseButtonText = "キャンセル",
    //            DefaultButton = ContentDialogButton.Primary,
    //            XamlRoot = App.MainWindow.Content.XamlRoot
    //        };

    //        var result = await dialog.ShowAsync();

    //        if ( result == ContentDialogResult.Primary ) {
    //            if ( !string.IsNullOrWhiteSpace( inputTextBox.Text ) && tag.Name != inputTextBox.Text ) {
    //                tag.Name = inputTextBox.Text;
    //                await _tagService.AddOrUpdateTagAsync( tag );
    //            }
    //        } else if ( result == ContentDialogResult.Secondary ) {
    //            await _tagService.DeleteTagAsync( tag );
    //            // UIからタグを削除する処理が必要
    //            RemoveTagFromUI( TagItems, tag );
    //        }
    //    }

    //    private bool RemoveTagFromUI( ObservableCollection<TagItem> tags, TagItem tagToRemove ) {
    //        if ( tags.Remove( tagToRemove ) ) {
    //            return true;
    //        }

    //        foreach ( var tag in tags ) {
    //            if ( RemoveTagFromUI( tag.Children, tagToRemove ) ) {
    //                return true;
    //            }
    //        }
    //        return false;
    //    }


    //    // MainViewModelから移管
    //    public async Task SaveTagsInClose() {
    //        await SaveTagsRecursively( TagItems );
    //    }

    //    private async Task SaveTagsRecursively( ObservableCollection<TagItem> tags ) {
    //        try {
    //            foreach ( var tag in tags ) {
    //                if ( tag.IsModified )
    //                    await _tagService.AddOrUpdateTagAsync( tag );
    //                if ( tag.Children.Any() )
    //                    await SaveTagsRecursively( tag.Children );
    //            }
    //        } catch ( Exception ex ) {
    //            System.Diagnostics.Debug.WriteLine( $"Error saving tags: {ex.Message}" );
    //        }
    //    }

    //    // MainViewModelから移管
    //    public void PrepareTagsForEditing( VideoItem? selectedItem ) {
    //        if ( selectedItem == null )
    //            return;

    //        var allTags = _tagService.GetTagsInOrder();
    //        var checkedTagIds = new HashSet<int>(selectedItem.VideoTagItems.Select(t => t.Id));

    //        foreach ( var tag in allTags ) {
    //            tag.IsChecked = checkedTagIds.Contains( tag.Id );
    //            tag.IsEditing = true;
    //        }
    //    }

    //    // MainViewModelから移管
    //    public async Task UpdateVideoTagSelection( VideoItem? targetItem ) {
    //        if ( targetItem == null )
    //            return;

    //        targetItem.VideoTagItems.Clear();
    //        var tmpTag = _tagService.GetTagsInOrder();
    //        foreach ( var tag in tmpTag ) {
    //            if ( tag.IsChecked ) {
    //                targetItem.VideoTagItems.Add( tag );
    //                if ( !targetItem.VideoTagItems.Any( t => t.Id == tag.Id ) ) {
    //                    await _databaseService.AddTagToVideoAsync( targetItem, tag );
    //                    tag.TagVideoItem.Add( targetItem );
    //                }
    //            } else {
    //                var tagToRemove = targetItem.VideoTagItems.FirstOrDefault(t => t.Id == tag.Id);
    //                if ( tagToRemove != null ) {
    //                    await _databaseService.RemoveTagFromVideoAsync( targetItem, tagToRemove );
    //                    tag.TagVideoItem.Remove( targetItem );
    //                }
    //            }
    //            tag.IsEditing = false;
    //        }
    //    }
    //    public List<TagItem> GetTagsInOrder() {
    //        var orderedTags = new List<TagItem>();
    //        void Traverse( IEnumerable<TagItem> tags ) {
    //            foreach ( var tag in tags ) {
    //                orderedTags.Add( tag );
    //                Traverse( tag.Children );
    //            }
    //        }
    //        Traverse( TagItems );
    //        return orderedTags;
    //    }
    }
}