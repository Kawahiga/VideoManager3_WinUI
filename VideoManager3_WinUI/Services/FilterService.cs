using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reflection.Emit;
using System.Xml.Linq;
using VideoManager3_WinUI.Models;

namespace VideoManager3_WinUI.Services {
    public class FilterService:INotifyPropertyChanged {
        public ObservableCollection<FilterItem> Filters { get; } = new ObservableCollection<FilterItem>();

        // タグの複数選択モード
        private bool _multiFilterEnabled = false;
        public bool MultiFilterEnabled {
            get => _multiFilterEnabled;
            set {
                if ( _multiFilterEnabled != value ) {
                    _multiFilterEnabled = value;
                    if ( _multiFilterEnabled ) {
                        ButtonColor = new SolidColorBrush( Colors.Pink );
                        ButtonText = "ON";
                    } else {
                        ButtonColor = new SolidColorBrush( Colors.Blue );
                        ButtonText = "OFFだよ";
                    }
                }
            }
        }

        // タグの複数選択モード制御用ボタンの背景色
        private Brush _buttonColor = new SolidColorBrush(Colors.Blue);
        public Brush ButtonColor {
            get => _buttonColor;
            set {
                if ( _buttonColor != value ) {
                    _buttonColor = value;
                    PropertyChanged?.Invoke( this, new PropertyChangedEventArgs( nameof( ButtonColor ) ) );
                }
            }
        }

        // タグの複数選択モードのテキスト
        private string _buttonText = "aaaaa";
        public string ButtonText {
            get => _buttonText;
            set {
                if ( _buttonText != value ) {
                    _buttonText = value;
                    PropertyChanged?.Invoke( this, new PropertyChangedEventArgs( nameof( ButtonText ) ) );
                }
            }
        }

        // タグの複数選択モードを有効にするコマンド
        public void ToggleFilterMulti() {
            MultiFilterEnabled = true;
        }

        public bool SetTagFilter( TagItem? tag ) {
            // tagがnullの場合は、タグフィルターを解除
            return UpdateFilter( FilterType.Tag, tag, tag?.Name, tag?.TextColor, tag?.Color, tag == null );
        }

        public bool SetArtistFilter( ArtistItem? artist ) {
            // artistがnullの場合は、アーティストフィルターを解除
            return UpdateFilter( FilterType.Artist, artist, artist?.Name, artist?.TextColor, artist?.ArtistColor, artist == null );
        }

        public bool SetSearchTextFilter( string? searchText ) {
            // searchTextが空の場合は、検索フィルターを解除
            return UpdateFilter( FilterType.SearchText, searchText, searchText, new SolidColorBrush( Colors.Black ), new SolidColorBrush( Colors.LightGray ), string.IsNullOrWhiteSpace( searchText ) );
        }

        /// <summary>
        /// フィルターの更新（追加または削除）を行う
        /// </summary>
        private bool UpdateFilter( FilterType type, object? value, string? label, Brush? textColor, Brush? backColor, bool shouldRemove ) {

            if ( type.Equals( FilterType.Tag ) && label != null && label.Equals( "全てのファイル" ) ) {
                // 全てのフィルターをリセット
                Filters.Clear();
                MultiFilterEnabled = false;
                return true;
            }

            var existingFilter = Filters.FirstOrDefault(f => f.Value == value);
            if ( existingFilter != null ) {
                // 既に設定済みの場合、有効/無効を反転させる
                existingFilter.IsActive = !existingFilter.IsActive;
                return true;
            }

            if ( !MultiFilterEnabled ) {
                // 複数フィルターが無効な場合、既存の同種フィルターを削除                 
                var existingFilterType = Filters.FirstOrDefault(f => f.Type == type);
                if ( existingFilterType != null ) {
                    // イベントハンドラの購読を解除
                    existingFilterType.PropertyChanged -= FilterItem_PropertyChanged;
                    Filters.Remove( existingFilterType );
                }
            } else if ( type == FilterType.SearchText ) {
                // 有効な場合、検索文字列での登録を止める
                return false;
            }

            if ( !shouldRemove && value != null && label != null ) {
                var newFilter = new FilterItem( type, value, label, textColor, backColor );
                // 新しいフィルターのPropertyChangedイベントを購読
                newFilter.PropertyChanged += FilterItem_PropertyChanged;

                // フィルター種別に基づいてソートされたリストを作成
                var sortedFilters = Filters.Append(newFilter).OrderBy(f => f.Type).ThenBy(f => f.Label).ToList();
                // 新しいフィルターを正しい位置に挿入
                Filters.Insert( sortedFilters.IndexOf( newFilter ), newFilter );
                return true;
            }
            return false;
        }

        /// <summary>
        /// 動画リストにフィルターを適用する
        /// </summary>
        public IEnumerable<VideoItem> ApplyFilters( IEnumerable<VideoItem> videos ) {
            var activeFilters = Filters.Where(f => f.IsActive == false).ToList();
            if ( !activeFilters.Any() )                 return videos;
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
            if ( selectedTag.Name.Equals( "タグなし" ) )                 return !video.VideoTagItems.Any();
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

        // フィルター状態が変更されたときに発生するイベント
        public event Action? FilterStateChanged;
        private void FilterItem_PropertyChanged( object? sender, PropertyChangedEventArgs e ) {
            // IsActiveプロパティが変更された場合のみイベントを発行
            if ( e.PropertyName == nameof( FilterItem.IsActive ) )                 FilterStateChanged?.Invoke();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        //protected virtual void OnPropertyChanged( string propertyName ) {
        //    PropertyChanged?.Invoke( this, new PropertyChangedEventArgs( propertyName ) );
        //}
    }
}
