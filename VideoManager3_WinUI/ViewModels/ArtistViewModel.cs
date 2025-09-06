using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VideoManager3_WinUI.Models;
using VideoManager3_WinUI.Services;

namespace VideoManager3_WinUI.ViewModels {
    public class ArtistViewModel:ObservableObject {
        // 表示させるアーティスト一覧
        public ObservableCollection<ArtistItem> Artists { get; } = new ObservableCollection<ArtistItem>();

        // アーティストリストで選択されているアーティスト
        private ArtistItem? _selectedArtist;
        public ArtistItem? SelectedArtist {
            get => _selectedArtist;
            set {
                if ( SetProperty( ref _selectedArtist, value ) ) {
                    SelectedArtistChanged?.Invoke( value );
                }
            }
        }

        public event Action<ArtistItem?>? SelectedArtistChanged;

        private ArtistService _artistService;

        public ArtistViewModel( ArtistService artistService ) {
            _artistService = artistService;
        }

        /// <summary>
        /// データベースからアーティスト情報を非同期にロードします。
        /// </summary>
        public async Task LoadArtists() {
            Artists.Clear();
            var artists = await _artistService.LoadArtistsAsync();
            foreach ( var artist in artists ) {
                Artists.Add( artist );
            }
        }
    }
}
