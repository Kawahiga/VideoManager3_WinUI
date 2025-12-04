using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using VideoManager3_WinUI.Models;

namespace VideoManager3_WinUI.Services {
    /// <summary>
    /// アーティスト情報の管理と処理を行うサービスクラスです。
    /// </summary>
    public class ArtistService {
        /// <summary>
        /// UIに表示されるアーティストのコレクションです。
        /// </summary>
        public ObservableCollection<ArtistItem> Artists { get; } = [];

        private readonly DatabaseService _databaseService;

        public ArtistService( DatabaseService databaseService ) {
            _databaseService = databaseService;
        }

        /// <summary>
        /// データベースからアーティスト情報を非同期にロードします。
        /// </summary>
        public async Task LoadArtistsAsync() {
            try {
                Artists.Clear();
                var artistsFromDb = await _databaseService.GetAllArtistsAsync();
                foreach ( var artist in artistsFromDb ) {
                    // 名前の正規化
                    // 例：浜崎りお （ 篠原絵梨香,森下えりか ） → 浜崎りお( 篠原絵梨香、森下えりか )
                    var normalizedName = artist.Name.Replace(" ", "").Replace("（", "(").Replace("）", ")").Replace(",", "、");
                    artist.Name = normalizedName;

                    // アーティスト名から別名義を抽出
                    var aliasMatch = Regex.Match( artist.Name, @"([^(（]+)[(（]([^)）]+)[)）]" );
                    if ( aliasMatch.Success ) {
                        string mainName = aliasMatch.Groups[1].Value.Trim();
                        string[] aliases = aliasMatch.Groups[2].Value.Split( new[] { '、' }, StringSplitOptions.RemoveEmptyEntries );
                        artist.AliaseNames = aliases.Select( a => a.Trim() ).ToList();
                        artist.AliaseNames.Insert( 0, mainName ); // 主名も含める
                    } else {
                        artist.AliaseNames = new List<string> { artist.Name };
                    }
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
                    // 既存の関連動画リストをクリア
                    artist.VideosInArtist.Clear();
                }
                foreach ( var video in videos ) {
                    // 既存の関連アーティストリストをクリア
                    video.ArtistsInVideo.Clear();
                }

                foreach ( var video in videos ) {
                    var artistsFromDb = await _databaseService.GetArtistsForVideoAsync(video);
                    foreach ( var artist in artistsFromDb ) {
                        // Artistsから同じIDのアーティストを探す
                        ArtistItem? existingArtist = Artists.FirstOrDefault(a => a.Id == artist.Id);
                        if ( existingArtist != null ) {
                            video.ArtistsInVideo.Add( existingArtist );
                            Artists.Where( a => a.Id == artist.Id ).FirstOrDefault()?.VideosInArtist.Add( video );
                        }
                    }
                }
                SortArtists();
            } catch ( Exception ex ) {
                System.Diagnostics.Debug.WriteLine( $"Error loading artist videos: {ex.Message}" );
            }
        }

        /// <summary>
        /// １つのビデオ情報をもとに、アーティストリストを更新します。
        /// 関連するアーティスト情報が既にあれば更新、なければ追加します。
        /// （アーティスト情報が変更されている場合、古い情報が残る）
        /// </summary>
        /// <param name="video">追加または更新するビデオ。</param>
        public async Task AddOrUpdateArtistFromVideoAsync( VideoItem video ) {
            // 既存の関連付けを解除。コレクション変更中のエラーを避けるため、コピーに対してループ処理を行う。
            foreach ( var artist in new List<ArtistItem>( video.ArtistsInVideo ) ) {
                await _databaseService.RemoveArtistFromVideoAsync( video, artist );
                artist.VideosInArtist.Remove( video );
            }
            video.ArtistsInVideo.Clear();

            // ビデオのファイル名を解析し、アーティスト情報を抽出して内部状態を更新する。
            string artistsString = GetArtistNameWithoutFileName( video.FileName ?? string.Empty );
            if ( string.IsNullOrEmpty( artistsString ) )
                return;

            // アーティストを分別（別名義を考慮）
            // 例: "浜崎りお(篠原絵梨香、森下えりか) 吉沢明歩" → ["浜崎りお(篠原絵梨香、森下えりか)", "吉沢明歩"]
            string pattern = @"\S+(\s*[\(（][^\)）]*[\)）])+|\S+";
            MatchCollection matches = Regex.Matches(artistsString, pattern);
            string[] extractedNames = matches.Cast<Match>().Select(m => m.Value).ToArray();

            var artistGroups = new List<List<string>>();
            foreach ( var nameGroup in extractedNames ) {
                // 別名義を分離
                // 例: "浜崎りお(篠原絵梨香、森下えりか)" → ["浜崎りお", "篠原絵梨香", "森下えりか"]
                var aliasMatch = Regex.Match(nameGroup, @"([^(（]+)[(（]([^)）]+)[)）]");
                var allNamesInGroup = new List<string>();

                if ( aliasMatch.Success ) {
                    string mainName = aliasMatch.Groups[1].Value.Trim();    // 主名義
                    allNamesInGroup.Add( mainName );
                    // 別名義を分割
                    string[] aliases = aliasMatch.Groups[2].Value.Split(new[] { '、', ',' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach ( var alias in aliases ) {
                        allNamesInGroup.Add( alias.Trim() );
                    }
                } else {
                    // 1つの名前だけの場合
                    allNamesInGroup.Add( nameGroup.Trim() );
                }
                artistGroups.Add( allNamesInGroup );
            }

            // アーティスト名をもとに、既存のArtistItemを検索または新規作成
            foreach ( var nameGroup in artistGroups ) {
                // nameGroup内のいずれかの名前を持つ既存のArtistItemを探す
                ArtistItem? existingArtist = Artists.FirstOrDefault(a => a.AliaseNames.Intersect(nameGroup, StringComparer.OrdinalIgnoreCase).Any());

                if ( existingArtist != null ) {
                    // --- 既存のアーティストが見つかった場合 ---

                    // 既存のAliaseNamesと新しい名前リスト(nameGroup)をマージ
                    var updatedAliases = existingArtist.AliaseNames.Union(nameGroup, StringComparer.OrdinalIgnoreCase).OrderBy(n => n).ToList();

                    // 主名義を決定（元の主名義を維持）
                    string mainName = existingArtist.AliaseNames.First();
                    var otherAliases = updatedAliases.Where(n => n != mainName).ToList();

                    // 表示名を更新
                    string displayName = mainName;
                    if ( otherAliases.Any() ) {
                        displayName += $"({string.Join( "、", otherAliases )})";
                    }

                    // ArtistItemを更新
                    existingArtist.Name = displayName;
                    existingArtist.AliaseNames = updatedAliases;

                    // ビデオとの関連付け
                    if ( !existingArtist.VideosInArtist.Contains( video ) ) {
                        existingArtist.VideosInArtist.Add( video );
                    }
                    if ( !video.ArtistsInVideo.Contains( existingArtist ) ) {
                        video.ArtistsInVideo.Add( existingArtist );
                    }

                    // データベース更新
                    await _databaseService.AddOrUpdateArtistAsync( existingArtist );
                    await _databaseService.AddArtistToVideoAsync( video, existingArtist );

                } else {
                    // --- 新しいアーティストを作成する場合 ---

                    string mainName = nameGroup.First();
                    var aliases = nameGroup.Skip(1).ToList();

                    // 表示名を作成
                    string displayName = mainName;
                    if ( aliases.Any() )
                        displayName += $"({string.Join( "、", aliases )})";

                    var newArtist = new ArtistItem
                    {
                        Name = displayName,
                        AliaseNames = nameGroup,
                    };

                    // ビデオとの関連付け
                    newArtist.VideosInArtist.Add( video );
                    video.ArtistsInVideo.Add( newArtist );

                    // UIコレクションに追加
                    Artists.Add( newArtist );

                    // データベースに新規追加
                    await _databaseService.AddOrUpdateArtistAsync( newArtist );
                    await _databaseService.AddArtistToVideoAsync( video, newArtist );
                }
            }
            SortArtists();
        }

        /// <summary>
        /// アーティスト情報をソートします。
        /// 1. お気に入り(あり→なし)、2. 動画数(多い順)、3. 名前(昇順)の優先順位でソートします。
        /// </summary>
        private void SortArtists() {
            var sorted = Artists
                .OrderByDescending( a => a.IsFavorite )               // お気に入りを優先
                .ThenByDescending( a => a.LikeCount )                // いいね数を優先
                .ThenByDescending( a => a.VideosInArtist.Count )      // 動画数の多い順
                .ThenBy( a => a.Name, StringComparer.OrdinalIgnoreCase ) // 名前順（大文字小文字無視）
                .ToList();
            Artists.Clear();
            foreach ( var artist in sorted ) {
                Artists.Add( artist );
            }
        }


        /// <summary>
        /// ファイル名からアーティスト名を抽出するユーティリティメソッド。
        /// </summary>
        public static string GetArtistNameWithoutFileName( string fileName ) {
            var match = Regex.Match( fileName, @"^[\[【](.*?)[\]】]" );
            if ( !match.Success )
                return string.Empty;
            return match.Groups[1].Value.Trim();
        }

        /// <summary>
        /// ファイル名からアーティスト情報を削除した文字列を取得するユーティリティメソッド。
        /// </summary>
        public static string GetFileNameWithoutArtist( string fileName ) {
            var match = Regex.Match( fileName, @"^[\[【](.*?)[\]】]\s*(.*)" );
            if ( match.Success )
                return match.Groups[2].Value.Trim();
            return fileName;
        }
    }
}
