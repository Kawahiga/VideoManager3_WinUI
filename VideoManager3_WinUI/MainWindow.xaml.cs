using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.Generic;

namespace VideoManager3_WinUI
{
    public sealed partial class MainWindow : Window
    {
        // ViewModelへの参照をプロパティとして公開します。
        // これにより、XAMLからの {x:Bind} が可能になります。
        public MainViewModel ViewModel { get; }

        public MainWindow()
        {
            this.InitializeComponent();
            
            // ViewModelのインスタンスを作成します。
            // DataContextへの設定は不要です。x:Bindはコードビハインドのプロパティを直接参照するためです。
            ViewModel = new MainViewModel();

            // TreeViewのItemsSourceにViewModelのTagItemsをバインドします。
            // これにより、PopulateTreeViewメソッドは不要になります。
            TagsTreeView.ItemsSource = ViewModel.TagItems;
        }

        // このメソッドはXAMLでのDataTemplateとItemsSourceへのバインディングに置き換えられたため、不要になりました。
        // private void PopulateTreeView(IEnumerable<TagItem> tagItems, IList<TreeViewNode> parentNodes)
        // {
        //     foreach (var tagItem in tagItems)
        //     {
        //         var node = new TreeViewNode()
        //         {
        //             Content = tagItem,
        //         };
        //         
        //         if (tagItem.Children != null && tagItem.Children.Count > 0)
        //         {
        //             PopulateTreeView(tagItem.Children, node.Children);
        //         }
        //
        //         parentNodes.Add(node);
        //     }
        // }
    }
}