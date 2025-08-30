using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VideoManager3_WinUI.Converters {

    /// <summary>
    /// いいねカウントの可視性を評価するコンバーター
    /// 0だったら表示しない
    /// </summary>
    internal class LikeCountConverter:IValueConverter {

        public object Convert( object value, Type targetType, object parameter, string language ) {
            if ( value is int likeCount ) {
                if ( likeCount > 0 ) {
                    return Visibility.Visible;
                }
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack( object value, Type targetType, object parameter, string language ) {
            throw new NotImplementedException();
        }
    }
}
