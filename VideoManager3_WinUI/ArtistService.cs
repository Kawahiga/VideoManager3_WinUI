using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace VideoManager3_WinUI {
    public class ArtistService {
        public ObservableCollection<ArtistItem> Artists { get; } = [];

        /// <summary>
        /// アーティスト一覧を作成
        /// </summary>
        public void CreateArtistList( ObservableCollection<VideoItem> videos ) {
            Artists.Clear();
            var artistsWithFiles = new Dictionary<string, List<string>>();

            foreach ( var video in videos ) {
                if ( video.FileName != null && video.FileName.StartsWith( "[" ) ) {
                    int endIndex = video.FileName.IndexOf(']');
                    if ( endIndex > 1 ) {
                        string artistsString = video.FileName.Substring(1, endIndex - 1);
                        
                        string pattern = @"\S+(\s*[\(（][^\)）]*[\)）])+|\S+";
                        MatchCollection matches = Regex.Matches(artistsString, pattern);
                        string[] artistNames = matches.Cast<Match>().Select(m => m.Value).ToArray();

                        foreach ( var name in artistNames ) {
                            if ( !artistsWithFiles.ContainsKey( name ) ) {
                                artistsWithFiles[name] = new List<string>();
                            }

                            if ( video.FileName != null && !artistsWithFiles[name].Contains( video.FileName ) ) {
                                artistsWithFiles[name].Add( video.FileName );
                            }
                        }
                    }
                }
            }

            var sortedArtistNames = artistsWithFiles.Keys.OrderBy(name => name);
            //var sortedArtistNames = artistsWithFiles.Keys.OrderByDescending(name => artistsWithFiles[name].Count);

            foreach ( var artistName in sortedArtistNames ) {
                Artists.Add( new ArtistItem
                {
                    Name = artistName,
                    SourceFileNames = artistsWithFiles[artistName]
                } );
            }
        }
    }
}