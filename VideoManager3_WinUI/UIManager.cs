using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace VideoManager3_WinUI
{
    public partial class UIManager : ObservableObject
    {
        [ObservableProperty]
        private bool _isGridView = true;

        public bool IsListView => !IsGridView;

        [ObservableProperty]
        private bool _isTreeView = true;

        public bool IsArtistView => !IsTreeView;

        [ObservableProperty]
        private double _thumbnailSize = 150.0;

        public double ThumbnailHeight => ThumbnailSize * 9.0 / 16.0;

        [ObservableProperty]
        private bool _isTagSetting = false;

        [RelayCommand]
        private void ToggleView()
        {
            IsGridView = !IsGridView;
        }

        partial void OnIsGridViewChanged(bool value)
        {
            OnPropertyChanged(nameof(IsListView));
        }

        partial void OnIsTreeViewChanged(bool value)
        {
            OnPropertyChanged(nameof(IsArtistView));
        }

        partial void OnThumbnailSizeChanged(double value)
        {
            OnPropertyChanged(nameof(ThumbnailHeight));
        }
    }
}