using Microsoft.UI.Xaml.Data;
using System;

namespace VideoManager3_WinUI.Converters
{
    public class DurationConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is double seconds)
            {
                var ts = TimeSpan.FromSeconds(seconds);
                if (ts.TotalHours >= 1)
                    return ts.ToString(@"hh\:mm\:ss");
                else
                    return ts.ToString(@"mm\:ss");
            }
            return "00:00";
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}