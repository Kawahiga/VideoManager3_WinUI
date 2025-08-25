using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;

namespace VideoManager3_WinUI {
    /// <summary>
    /// アーティスト情報の管理と処理を行うサービスクラスです。
    /// </summary>
    public class ArtistService {
        /// <summary>
        /// UIに表示されるアーティストのコレクションです。
        /// </summary>
        public ObservableCollection<ArtistItem> Artists { get; } = [];

        private readonly DatabaseService _databaseService;

        /// <summary>
        /// アーティスト間の関連性を管理するUnion-Find構造。
        /// </summary>
        private readonly UnionFind _artistUnionFind = new UnionFind();

        /// <summary>
        /// アーティスト名（別名含む）と、それに関連するビデオのリスト。
        /// </summary>
        private readonly Dictionary<string, List<VideoItem>> _videosByArtistName = new Dictionary<string, List<VideoItem>>();


        public ArtistService( DatabaseService databaseService ) {
            _databaseService = databaseService;
        }


        /// <summary>
        /// アーティストのグループ化（同一性の判定）を行うためのデータ構造です。
        /// Union-Find（またはDisjoint Set Union）アルゴリズムを実装しています。
        /// </summary>
        private class UnionFind {
            // 各要素（アーティスト名）の親要素を保持します。
            private Dictionary<string, string> _parent;

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

            /// <summary>
            /// 内部状態をクリアします。
            /// </summary>
            public void Clear() {
                _parent.Clear();
            }
        }

        /// <summary>
        /// ビデオのリストからアーティスト一覧を完全に再作成します。
        /// 既存のアーティスト情報はすべてクリアされます。
        /// </summary>
        public void CreateArtistList( IEnumerable<VideoItem> videos ) {
            // 状態をリセット
            _artistUnionFind.Clear();
            _videosByArtistName.Clear();

            foreach ( var video in videos ) {
                video.ArtistsInVideo.Clear();
            }

            // 各ビデオの情報を解析して状態を構築
            foreach ( var video in videos ) {
                ParseArtistsFromVideo( video );
            }

            // 構築した状態からUI向けのコレクションを生成
            UpdateArtistsCollection();
        }

        /// <summary>
        /// １つのビデオ情報を既存のアーティストリストに追加・統合します。
        /// </summary>
        public void AddOrUpdateArtistFromVideo( VideoItem video ) {
            // もしこのビデオが以前処理されていた場合、古い情報を削除する
            RemoveVideoFromState( video );
            video.ArtistsInVideo.Clear();

            // ビデオを解析して状態を更新
            ParseArtistsFromVideo( video );

            // 更新された状態からUI向けのコレクションを再生成
            UpdateArtistsCollection();
        }

        /// <summary>
        /// 指定されたビデオの情報を内部状態から削除します。
        /// </summary>
        private void RemoveVideoFromState( VideoItem video ) {
            var keysToRemove = new List<string>();
            var artistNamesInVideo = _videosByArtistName
                .Where(kvp => kvp.Value.Contains(video))
                .Select(kvp => kvp.Key)
                .ToList();

            foreach ( var artistName in artistNamesInVideo ) {
                _videosByArtistName[artistName].Remove( video );
                if ( _videosByArtistName[artistName].Count == 0 ) {
                    _videosByArtistName.Remove( artistName );
                }
            }
            // 注意: UnionFindからは現状削除は行わない。
            // アーティスト名がどこにも使われなくなってもUnionFind内に残るが、
            // 新しいビデオで同じ名前が使われれば再利用されるため、実害は少ない。
        }


        /// <summary>
        /// １つのビデオファイル名を解析し、アーティスト情報を内部状態(_artistUnionFind, _videosByArtistName)に格納します。
        /// 同時に、VideoItemのFileNameWithoutArtistsプロパティも設定します。
        /// </summary>
        private void ParseArtistsFromVideo( VideoItem video ) {
            if ( video.FileName == null ) {
                video.FileNameWithoutArtists = string.Empty;
                return;
            }

            // アーティスト情報が `[artist]` または `【artist】` の形式でファイル名の先頭にあると仮定
            var match = Regex.Match(video.FileName, @"^[\[【](.*?)[\]】]\s*(.*)");

            if ( !match.Success ) {
                // アーティスト情報が見つからない場合は、ファイル名全体をそのまま使用
                video.FileNameWithoutArtists = video.FileName;
                return;
            }

            // アーティスト情報を抽出
            string artistsString = match.Groups[1].Value;
            // アーティスト部分を除いたファイル名を設定
            video.FileNameWithoutArtists = match.Groups[2].Value.Trim();

            // --- 以下、アーティスト名の解析処理 ---

            string pattern = @"\\S+(\\s*[\\(（][^\\)）]*[\\)）])+|\\S+";
            MatchCollection matches = Regex.Matches(artistsString, pattern);
            string[] extractedNames = matches.Cast<Match>().Select(m => m.Value).ToArray();

            foreach ( var nameGroup in extractedNames ) {
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

                foreach ( var name in allNamesInGroup ) {
                    _artistUnionFind.Add( name );
                    if ( !_videosByArtistName.ContainsKey( name ) ) {
                        _videosByArtistName[name] = new List<VideoItem>();
                    }
                    if ( !_videosByArtistName[name].Contains( video ) ) {
                        _videosByArtistName[name].Add( video );
                    }
                }

                if ( allNamesInGroup.Count > 1 ) {
                    for ( int i = 1; i < allNamesInGroup.Count; i++ ) {
                        _artistUnionFind.Union( allNamesInGroup[0], allNamesInGroup[i] );
                    }
                }
            }
        }

        /// <summary>
        /// 現在の内部状態から、UIに表示するArtistsコレクションを再構築します。
        /// </summary>
        private void UpdateArtistsCollection() {
            try {
                // --- マージ処理 ---
                var mergedArtists = new Dictionary<string, List<VideoItem>>();
                foreach ( var artistName in _videosByArtistName.Keys ) {
                    string root = _artistUnionFind.Find(artistName);
                    if ( !mergedArtists.ContainsKey( root ) ) {
                        mergedArtists[root] = new List<VideoItem>();
                    }
                    foreach ( var video in _videosByArtistName[artistName] ) {
                        if ( !mergedArtists[root].Contains( video ) ) {
                            mergedArtists[root].Add( video );
                        }
                    }
                }

                // --- ArtistItem作成処理 ---
                var newArtistList = new List<ArtistItem>();
                var groups = _artistUnionFind.GetGroups();
                var sortedRoots = mergedArtists.Keys.OrderByDescending(root => mergedArtists[root].Count).ThenBy(r => r);

                foreach ( var rootName in sortedRoots ) {
                    var groupMembers = groups.ContainsKey(rootName) ? groups[rootName] : new List<string> { rootName };

                    string displayRoot = groupMembers
                        .OrderByDescending(m => _videosByArtistName.ContainsKey(m) ? _videosByArtistName[m].Count : 0)
                        .ThenBy(m => m, StringComparer.Ordinal)
                        .FirstOrDefault() ?? rootName;

                    var aliases = groupMembers.Where(m => m != displayRoot).OrderBy(a => a).ToList();

                    string displayName = displayRoot;
                    if ( aliases.Any() ) {
                        displayName += $"({string.Join( "、", aliases )})";
                    }

                    var newArtist = new ArtistItem
                    {
                        Name = displayName,
                        VideosInArtist = mergedArtists[rootName],
                    };

                    newArtistList.Add( newArtist );
                }

                // --- UIコレクションとビデオの関連付け更新 ---
                Artists.Clear();
                foreach ( var artist in newArtistList ) {
                    Artists.Add( artist );
                    foreach ( var video in artist.VideosInArtist ) {
                        if ( !video.ArtistsInVideo.Contains( artist ) ) {
                            video.ArtistsInVideo.Add( artist );
                        }
                    }
                }
            } catch ( Exception ex ) {
                System.Diagnostics.Debug.WriteLine( $"Error in UpdateArtistsCollection: {ex.Message}" );
                Artists.Clear();
            }
        }

        /// <summary>
        /// データベースからアーティスト情報を非同期にロードします。
        /// </summary>
        public async Task LoadArtistsAsync() {
            try {
                Artists.Clear();
                var artistsFromDb = await _databaseService.GetAllArtistsAsync();
                foreach ( var artist in artistsFromDb ) {
                    Artists.Add( artist );
                }
            } catch ( Exception ex ) {
                System.Diagnostics.Debug.WriteLine( $"Error loading artists: {ex.Message}" );
            }
        }

        /// <summary>
        /// データベースからアーティストと動画の関連情報を非同期にロードします。
        /// </summary>
        public async Task LoadArtistVideosAsync( ObservableCollection<VideoItem> videos ) {
            try {
                foreach ( var artist in Artists ) {
                    artist.VideosInArtist.Clear();
                }

                foreach ( var video in videos ) {
                    video.ArtistsInVideo.Clear();
                    var artistsFromDb = await _databaseService.GetArtistsForVideoAsync(video);
                    if ( artistsFromDb != null && artistsFromDb.Count > 0 ) {
                        foreach ( var artist in artistsFromDb ) {
                            video.ArtistsInVideo.Add( artist );
                            Artists.Where( a => a.Id == artist.Id )
                                   .FirstOrDefault()?.VideosInArtist.Add( video );
                        }
                    }
                }
            } catch ( Exception ex ) {
                System.Diagnostics.Debug.WriteLine( $"Error loading artist videos: {ex.Message}" );
            }
        }
    }
}