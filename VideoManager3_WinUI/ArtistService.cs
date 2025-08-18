using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI;

namespace VideoManager3_WinUI {

    public enum ArtistSortKey {
        Name,
        VideoCount
    }

    public enum SortOrder {
        Ascending,
        Descending
    }

    public class ArtistService {
        public ObservableCollection<ArtistItem> Artists { get; } = [];

        /// <summary>
        /// アーティスト一覧を作成
        /// </summary>
        public void CreateArtistList( ObservableCollection<VideoItem> videos, ObservableCollection<TagItem> tags ) {
            Artists.Clear();
            foreach ( var video in videos ) {
                video.ArtistsInVideo.Clear();
            }
            // アーティスト名と、そのアーティストが含まれる動画のリストを紐づけるための一時的な辞書
            var artistsWithVideos = new Dictionary<string, List<VideoItem>>();

            foreach ( var video in videos ) {
                if ( video.FileName != null && video.FileName.StartsWith( "[" ) ) {
                    int endIndex = video.FileName.IndexOf(']');
                    if ( endIndex > 1 ) {
                        // ']' が見つかった場合 '[' と ']' の間の文字列（アーティスト名が含まれる部分）を抽出
                        string artistsString = video.FileName.Substring(1, endIndex - 1);

                        // 複数のアーティスト名を区切るための正規表現パターン
                        // 例: "[artist1 artist2 (group)]" のような形式に対応
                        string pattern = @"\S+(\s*[\(（][^\)）]*[\)）])+|\S+";
                        MatchCollection matches = Regex.Matches(artistsString, pattern);
                        string[] artistNames = matches.Cast<Match>().Select(m => m.Value).ToArray();

                        // 抽出したアーティスト名でループ
                        foreach ( var name in artistNames ) {
                            // 辞書にアーティスト名がまだ登録されていなければ、新しいリストを作成して登録
                            if ( !artistsWithVideos.ContainsKey( name ) ) {
                                artistsWithVideos[name] = new List<VideoItem>();
                            }

                            // まだ登録されていない動画であれば、そのアーティストの動画リストに追加
                            if ( !artistsWithVideos[name].Contains( video ) ) {
                                artistsWithVideos[name].Add( video );
                            }
                        }
                    }
                }
            }

            // 動画数が多い順（降順）にアーティスト名をソート
            var sortedArtistNames = artistsWithVideos.Keys.OrderByDescending(name => artistsWithVideos[name].Count);

            // ソートされた順序で、最終的なアーティストリストを作成
            foreach ( var artistName in sortedArtistNames ) {

                Brush artistColor;
                var matchingTag = FindTagByName(tags, artistName);
                if ( matchingTag != null ) {
                    // 同名のタグが存在する場合は、アーティスト色をピンクに設定
                    artistColor = new SolidColorBrush( Colors.DeepPink );
                } else {
                    // タグが見つからない場合は、DarkGray色を設定
                    artistColor = new SolidColorBrush( Colors.DarkGray );
                }

                var newArtist = new ArtistItem
                {
                    Name = artistName,
                    VideosInArtist = artistsWithVideos[artistName],
                    ArtistColor = artistColor,
                };

                Artists.Add( newArtist );

                // このアーティストが含まれるビデオに、このArtistItemを関連付ける
                foreach ( var video in newArtist.VideosInArtist ) {
                    video.ArtistsInVideo.Add( newArtist );
                }
            }
        }

        /// <summary>
        /// タグ名でタグを検索します。将来的にはTagServiceに移動する。
        /// </summary>
        private TagItem? FindTagByName( IEnumerable<TagItem> tags, string name ) {
            foreach ( var tag in tags ) {
                if ( tag.Name.Equals( name, StringComparison.OrdinalIgnoreCase ) ) {
                    return tag;
                }
                var foundInChild = FindTagByName(tag.Children, name);
                if ( foundInChild != null ) {
                    return foundInChild;
                }
            }
            return null;
        }

        /// <summary>
        /// アーティスト一覧をソートします。
        /// </summary>
        public void SortArtists( ArtistSortKey sortKey, SortOrder sortOrder ) {
            List<ArtistItem> sortedList;

            switch ( sortKey ) {
                case ArtistSortKey.Name:
                // ソートキーが名前の場合
                // 昇順か降順かで並べ替え
                sortedList = (sortOrder == SortOrder.Ascending)
                    ? Artists.OrderBy( a => a.Name ).ToList()
                    : Artists.OrderByDescending( a => a.Name ).ToList();
                break;

                case ArtistSortKey.VideoCount:
                // ソートキーが動画数の場合
                // 昇順か降順かで並べ替え。動画数が同じ場合は名前順でソート
                sortedList = (sortOrder == SortOrder.Ascending)
                    ? Artists.OrderBy( a => a.VideosInArtist.Count ).ThenBy( a => a.Name ).ToList()
                    : Artists.OrderByDescending( a => a.VideosInArtist.Count ).ThenBy( a => a.Name ).ToList();
                break;
                default:
                throw new ArgumentException( "Invalid sort key specified." );
            }

            // 現在のリストをクリア
            Artists.Clear();
            // ソート済みのリストを再度追加
            foreach ( var item in sortedList ) {
                Artists.Add( item );
            }
        }
    }
}