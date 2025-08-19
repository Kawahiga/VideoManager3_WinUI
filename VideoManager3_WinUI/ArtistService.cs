using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;

namespace VideoManager3_WinUI {
    /// <summary>
    /// アーティストリストのソートに使用するキーを指定します。
    /// </summary>
    public enum ArtistSortKey {
        Name,       // 名前でソート
        VideoCount  // 紐づく動画数でソート
    }

    /// <summary>
    /// ソートの順序を指定します。
    /// </summary>
    public enum SortOrder {
        Ascending,  // 昇順
        Descending // 降順
    }

    /// <summary>
    /// アーティスト情報の管理と処理を行うサービスクラスです。
    /// </summary>
    public class ArtistService {
        /// <summary>
        /// UIに表示されるアーティストのコレクションです。
        /// </summary>
        public ObservableCollection<ArtistItem> Artists { get; } = [];

        /// <summary>
        /// アーティストのグループ化（同一性の判定）を行うためのデータ構造です。
        /// Union-Find（またはDisjoint Set Union）アルゴリズムを実装しています。
        /// </summary>
        private class UnionFind {
            // 各要素（アーティスト名）の親要素を保持します。
            private readonly Dictionary<string, string> _parent;

            public UnionFind() {
                _parent = new Dictionary<string, string>();
            }

            /// <summary>
            /// 新しい要素をセットに追加します。最初は自分自身を親とします。
            /// </summary>
            public void Add( string name ) {
                if ( !_parent.ContainsKey( name ) ) {
                    _parent[name] = name;
                }
            }

            /// <summary>
            /// 指定した要素の根（グループの代表元）を見つけます。
            /// 途中の要素を根に直接つなぎ直す「経路圧縮」で効率化しています。
            /// </summary>
            public string Find( string name ) {
                if ( !_parent.ContainsKey( name ) ) {
                    Add( name );
                    return name;
                }

                if ( _parent[name] == name ) {
                    return name;
                }

                return _parent[name] = Find( _parent[name] );
            }

            /// <summary>
            /// 2つの要素が含まれるグループを統合します。
            /// 代表元を辞書順で若い方に統一し、動作の安定性を確保します。
            /// </summary>
            public void Union( string name1, string name2 ) {
                string root1 = Find(name1);
                string root2 = Find(name2);
                if ( root1 != root2 ) {
                    if ( string.Compare( root1, root2, StringComparison.Ordinal ) < 0 ) {
                        _parent[root2] = root1;
                    } else {
                        _parent[root1] = root2;
                    }
                }
            }

            /// <summary>
            /// 全てのグループを、代表元をキーとしたメンバーのリストとして取得します。
            /// </summary>
            public Dictionary<string, List<string>> GetGroups() {
                var groups = new Dictionary<string, List<string>>();
                foreach ( var name in _parent.Keys ) {
                    string root = Find(name);
                    if ( !groups.ContainsKey( root ) ) {
                        groups[root] = new List<string>();
                    }
                    groups[root].Add( name );
                }
                return groups;
            }
        }

        /// <summary>
        /// ビデオのリストからアーティスト一覧を作成します。
        /// 別名義を認識し、関連するアーティストをグループ化して表示します。
        /// </summary>
        public void CreateArtistList( ObservableCollection<VideoItem> videos, ObservableCollection<TagItem> tags ) {
            try {
                Artists.Clear();
                foreach ( var video in videos ) {
                    video.ArtistsInVideo.Clear();
                }

                // --- パース処理 --- 
                // 1. 全てのアーティスト名（別名含む）と、それに対応するビデオのリストを作成します。
                // 2. 同時に、Union-Find構造を使ってアーティスト間の関連（グループ）を構築します。
                var artistsWithVideos = new Dictionary<string, List<VideoItem>>();
                var uf = new UnionFind();

                foreach ( var video in videos ) {
                    if ( video.FileName != null && video.FileName.StartsWith( "[" ) ) {
                        int endIndex = video.FileName.IndexOf(']');
                        if ( endIndex > 1 ) {
                            string artistsString = video.FileName.Substring(1, endIndex - 1);
                            // アーティスト名を抽出する正規表現: `Artist1(Alias1)` のような形式を一つの塊として捉えます。
                            string pattern = @"\S+(\s*[\(（][^\)）]*[\)）])+|\S+";
                            MatchCollection matches = Regex.Matches(artistsString, pattern);
                            string[] extractedNames = matches.Cast<Match>().Select(m => m.Value).ToArray();

                            foreach ( var nameGroup in extractedNames ) {
                                // `Artist1(Alias1, Alias2)` のような形式をパースして、主名と別名のリストに分解します。
                                var aliasMatch = Regex.Match(nameGroup, @"([^(（]+)[(（]([^)）]+)[)）]");
                                var allNamesInGroup = new List<string>();

                                if ( aliasMatch.Success ) {
                                    string mainName = aliasMatch.Groups[1].Value.Trim();
                                    allNamesInGroup.Add( mainName );
                                    string[] aliases = aliasMatch.Groups[2].Value.Split(new[] { '、', ',' }, StringSplitOptions.RemoveEmptyEntries);
                                    foreach ( var alias in aliases ) {
                                        allNamesInGroup.Add( alias.Trim() );
                                    }
                                } else {
                                    allNamesInGroup.Add( nameGroup.Trim() );
                                }

                                // パースした全ての名前をUnion-Find構造に追加し、ビデオと紐付けます。
                                foreach ( var name in allNamesInGroup ) {
                                    uf.Add( name );
                                    if ( !artistsWithVideos.ContainsKey( name ) ) {
                                        artistsWithVideos[name] = new List<VideoItem>();
                                    }
                                    if ( !artistsWithVideos[name].Contains( video ) ) {
                                        artistsWithVideos[name].Add( video );
                                    }
                                }

                                // 同じ括弧内に含まれるアーティスト同士を同じグループとして統合します。
                                if ( allNamesInGroup.Count > 1 ) {
                                    for ( int i = 1; i < allNamesInGroup.Count; i++ ) {
                                        uf.Union( allNamesInGroup[0], allNamesInGroup[i] );
                                    }
                                }
                            }
                        }
                    }
                }

                // --- マージ処理 ---
                // Union-Findで構築したグループ情報に基づき、各アーティストのビデオをグループの代表（root）に集約します。
                var mergedArtists = new Dictionary<string, List<VideoItem>>();
                foreach ( var artistName in artistsWithVideos.Keys ) {
                    string root = uf.Find(artistName);
                    if ( !mergedArtists.ContainsKey( root ) ) {
                        mergedArtists[root] = new List<VideoItem>();
                    }
                    // ビデオリストをマージ（重複を排除しながら追加）
                    foreach ( var video in artistsWithVideos[artistName] ) {
                        if ( !mergedArtists[root].Contains( video ) ) {
                            mergedArtists[root].Add( video );
                        }
                    }
                }

                // --- ArtistItem作成処理 ---
                // 最終的なアーティストリストをUI向けに作成します。
                var groups = uf.GetGroups();
                // 表示順序：ビデオ数が多い順、次に名前の辞書順
                var sortedRoots = mergedArtists.Keys.OrderByDescending(root => mergedArtists[root].Count).ThenBy(r => r);

                foreach ( var rootName in sortedRoots ) {
                    var groupMembers = groups.ContainsKey(rootName) ? groups[rootName] : new List<string> { rootName };

                    // --- 主名の決定 ---
                    // グループ内で最もビデオ数が多いアーティストを主名（表示名）とします。
                    // ビデオ数が同じ場合は、名前の辞書順で決定します。
                    string displayRoot = groupMembers
                        .OrderByDescending(m => artistsWithVideos.ContainsKey(m) ? artistsWithVideos[m].Count : 0)
                        .ThenBy(m => m, StringComparer.Ordinal)
                        .FirstOrDefault() ?? rootName;

                    var aliases = groupMembers.Where(m => m != displayRoot).OrderBy(a => a).ToList();

                    string displayName = displayRoot;
                    if ( aliases.Any() ) {
                        displayName += $"({string.Join( "、", aliases )})";
                    }

                    // アーティスト名またはその別名義のいずれかがタグと一致すれば、色を変更します。
                    Brush artistColor = new SolidColorBrush(Colors.DarkGray); // デフォルトはDarkGray
                    foreach ( var memberName in groupMembers ) {
                        if ( FindTagByName( tags, memberName ) != null ) {
                            artistColor = new SolidColorBrush( Colors.DeepPink );
                            break; // タグが見つかったらループを抜ける
                        }
                    }

                    var newArtist = new ArtistItem
                    {
                        Name = displayName,
                        VideosInArtist = mergedArtists[rootName], // ビデオはグループ（rootName）のものを設定
                        ArtistColor = artistColor,
                    };

                    Artists.Add( newArtist );

                    // 各ビデオに、作成したArtistItemを関連付けます。
                    foreach ( var video in newArtist.VideosInArtist ) {
                        video.ArtistsInVideo.Add( newArtist );
                    }
                }
            } catch ( Exception ex ) {
                // エラーが発生した場合は、アプリがクラッシュしないように例外を捕捉します。
                System.Diagnostics.Debug.WriteLine( $"Error in CreateArtistList: {ex.Message}" );
                Artists.Clear(); // リストをクリアして、不正な状態を防ぎます。
            }
        }

        /// <summary>
        /// タグのコレクションから、指定した名前のタグを再帰的に検索します。
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
        /// アーティストリストを指定されたキーと順序でソートします。
        /// </summary>
        public void SortArtists( ArtistSortKey sortKey, SortOrder sortOrder ) {
            List<ArtistItem> sortedList;

            switch ( sortKey ) {
                case ArtistSortKey.Name:
                sortedList = (sortOrder == SortOrder.Ascending)
                    ? Artists.OrderBy( a => a.Name ).ToList()
                    : Artists.OrderByDescending( a => a.Name ).ToList();
                break;

                case ArtistSortKey.VideoCount:
                sortedList = (sortOrder == SortOrder.Ascending)
                    ? Artists.OrderBy( a => a.VideosInArtist.Count ).ThenBy( a => a.Name ).ToList()
                    : Artists.OrderByDescending( a => a.VideosInArtist.Count ).ThenBy( a => a.Name ).ToList();
                break;
                default:
                throw new ArgumentException( "Invalid sort key specified." );
            }

            // 現在のコレクションをクリアし、ソート済みのリストで再構築します。
            Artists.Clear();
            foreach ( var item in sortedList ) {
                Artists.Add( item );
            }
        }
    }
}
