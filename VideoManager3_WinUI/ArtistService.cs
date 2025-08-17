using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VideoManager3_WinUI {
    internal class ArtistService {
        public ObservableCollection<ArtistItem> Artists { get; } = [];


        /// <summary>
        /// アーティスト一覧を作成
        /// </summary>
        public void CreateArtistList( ObservableCollection<VideoItem> videos ) {
            foreach ( VideoItem video in videos ) {
                // ファイル名の先頭が"["から始まるファイルのみ対象
                
                // 終端文字"]"までの文字列を半角スペース区切りでリストに格納
                
            }
        }
    }
}
