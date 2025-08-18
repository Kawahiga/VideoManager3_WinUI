using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VideoManager3_WinUI {
    public class ArtistItem {
        public string Name { get; set; } = string.Empty;
        public List<VideoItem> VideosInArtist { get; set; } = new List<VideoItem>();
    }
}
