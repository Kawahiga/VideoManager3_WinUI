using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.Generic;

namespace VideoManager3_WinUI
{
    public sealed partial class MainWindow : Window
    {
        public MainViewModel ViewModel { get; }

        public MainWindow()
        {
            this.InitializeComponent();
            ViewModel = new MainViewModel(DispatcherQueue.GetForCurrentThread());
            // Window.ContentをFrameworkElementにキャストしてDataContextを設定
            (this.Content as FrameworkElement)!.DataContext = ViewModel;


            // 【修正点】
            // XAMLで名前を付けたTagsTreeViewコントロールのRootNodesプロパティに
            // 直接データを設定する
           PopulateTreeView(ViewModel.TagItems, TagsTreeView.RootNodes);
        }

        // TagItemのコレクションからTreeViewNodeの階層を再帰的に作成するメソッド
        private void PopulateTreeView(IEnumerable<TagItem> tagItems, IList<TreeViewNode> parentNodes)
        {
            foreach (var tagItem in tagItems)
            {
                // 各TagItemをContentに持つTreeViewNodeを作成
                var node = new TreeViewNode()
                {
                    Content = tagItem,
                };

                // 子要素があれば、再帰的に処理
                if (tagItem.Children != null && tagItem.Children.Count > 0)
                {
                    PopulateTreeView(tagItem.Children, node.Children);
                }

                parentNodes.Add(node);
            }
        }





    }
}