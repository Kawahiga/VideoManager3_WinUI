using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VideoManager3_WinUI {
    internal class ArtistService {
        public ObservableCollection<ArtistItem> Artists { get; } = new ObservableCollection<ArtistItem>();
    }
}
