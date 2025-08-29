using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VideoManager3_WinUI.command {
    public class ToggleFilterMultiCommand {

        /// <summary>
        /// タグ・アーティストの複数選択モードを切り替えるコマンド
        /// </summary>
        public static void ToggleFilterMulti( FilterService filter ) {
            // タグの複数選択モードを切り替え
            filter.MultiFilterEnabled = !filter.MultiFilterEnabled;
        }

    }
}
