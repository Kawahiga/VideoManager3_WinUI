using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VideoManager3_WinUI {
    internal class ArtistItem {
        public string Name { get; set; } = string.Empty;
        public List<string> SourceFileNames { get; set; } = new List<string>();
    }
}
