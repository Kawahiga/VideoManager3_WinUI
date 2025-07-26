using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;

namespace VideoManager3_WinUI
{
    // bool値をVisibilityに変換するコンバーター
    public class BooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return (value is bool && (bool)value) ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return (value is Visibility && (Visibility)value == Visibility.Visible);
        }
    }
}
