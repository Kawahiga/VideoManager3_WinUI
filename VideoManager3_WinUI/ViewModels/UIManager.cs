using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace VideoManager3_WinUI {
    public partial class UIManager:ObservableObject {
        private bool _isGridView = true;
        public bool IsGridView {
            get => _isGridView;
            set {
                if ( SetProperty( ref _isGridView, value ) ) {
                    OnPropertyChanged( nameof( IsListView ) );
                }
            }
        }

        public bool IsListView => !IsGridView;

        private bool _isTreeView = true;
        public bool IsTreeView {
            get => _isTreeView;
            set {
                if ( SetProperty( ref _isTreeView, value ) ) {
                    OnPropertyChanged( nameof( IsArtistView ) );
                }
            }
        }
        public bool IsArtistView => !IsTreeView;

        private double _thumbnailSize = 150.0;
        public double ThumbnailSize {
            get => _thumbnailSize;
            set {
                if ( SetProperty( ref _thumbnailSize, value ) ) {
                    OnPropertyChanged( nameof( ThumbnailHeight ) );
                }
            }
        }
        public double ThumbnailHeight => ThumbnailSize * 9.0 / 16.0;

        private bool _isTagSetting = false;
        public bool IsTagSetting {
            get => _isTagSetting;
            set => SetProperty( ref _isTagSetting, value );
        }


        [RelayCommand]
        private void ToggleView() {
            IsGridView = !IsGridView;
        }
    }
}