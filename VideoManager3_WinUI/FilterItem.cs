using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;

namespace VideoManager3_WinUI {
    // フィルターの種類
    public enum FilterType {
        Tag,
        Artist,
        SearchText
    }

    public class FilterItem:INotifyPropertyChanged {
        public FilterType Type { get; }
        public object Value { get; }

        private string _label;
        public string Label {
            get => _label;
            set {
                if ( _label != value ) {
                    _label = value;
                    OnPropertyChanged( nameof( Label ) );
                }
            }
        }

        private bool _isActive;
        public bool IsActive {
            get => _isActive;
            set {
                if ( _isActive != value ) {
                    _isActive = value;
                    OnPropertyChanged( nameof( IsActive ) );
                }
            }
        }

        // フィルター実行コマンド
        public ICommand ApplyCommand { get; private set; }

        public event PropertyChangedEventHandler? PropertyChanged;

        public FilterItem( FilterType type, object value, string label, Action<FilterItem, ObservableCollection<VideoItem>> apply ) {
            Type = type;
            Value = value;
            _label = label;
            _isActive = true; // デフォルトでアクティブ
            ApplyCommand = new RelayCommand<ObservableCollection<VideoItem>>( ( videos ) => apply( this, videos ) );
        }

        protected virtual void OnPropertyChanged( string propertyName ) {
            PropertyChanged?.Invoke( this, new PropertyChangedEventArgs( propertyName ) );
        }
    }
}
