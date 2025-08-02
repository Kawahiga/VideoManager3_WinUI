using Microsoft.UI.Xaml.Data;
using System;

namespace VideoManager3_WinUI
{
    public class FileSizeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is long bytes)
            {
                string[] sizes = { "B", "KB", "MB", "GB", "TB" };
                int order = 0;
                double len = bytes;
                while (len >= 1024 && order < sizes.Length - 1)
                {
                    order++;
                    len /= 1024;
                }
                return $"{len:0.##} {sizes[order]}";
            }
            return "0 B";
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}