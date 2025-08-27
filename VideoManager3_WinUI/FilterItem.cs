using CommunityToolkit.Mvvm.Input;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
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

        // フィルターの有効/無効状態
        // ON/OFFとTrue/Falseを逆にしているので注意
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

        private Brush _textColor = new SolidColorBrush( Colors.Pink );
        public Brush TextColor {
            get => _textColor;
            set {
                if ( _textColor != value ) {
                    _textColor = value;
                    OnPropertyChanged( nameof( TextColor ) );
                }
            }
        }

        private Brush _backColor = new SolidColorBrush( Colors.Pink );
        public Brush BackColor {
            get => _backColor;
            set {
                if ( _backColor != value ) {
                    _backColor = value;
                    OnPropertyChanged( nameof( BackColor ) );
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public FilterItem( FilterType type, object value, string label, Brush? textColor, Brush? backgroundColor ) {
            Type = type;
            Value = value;
            _label = label;
            _isActive = false; // デフォルトでアクティブ
            if ( textColor != null ) {
                _textColor = textColor;
            }
            if ( backgroundColor != null ) {
                _backColor = backgroundColor;
            }
        }

        protected virtual void OnPropertyChanged( string propertyName ) {
            PropertyChanged?.Invoke( this, new PropertyChangedEventArgs( propertyName ) );
        }
    }
}
