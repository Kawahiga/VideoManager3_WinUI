using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;


// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace FileMover {
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow:Window {
        public MainWindow() {
            InitializeComponent();
        }

        // FileMoverのViewModel内
        public async Task StartMoveProcessAsync( MoveTaskInfo taskInfo ) {
            await Task.Run( async () =>
            {
                foreach ( var sourceFile in taskInfo.SourceFiles ) {
                    var destinationFile = Path.Combine(taskInfo.DestinationFolder, Path.GetFileName(sourceFile));

                    // 進捗報告用のオブジェクト
                    var progress = new Progress<long>(bytesCopied => {
                        // ここでUIに進捗を反映させるプロパティを更新
                        CurrentFileProgress = (double)bytesCopied / TotalFileSize;
                    });

                    await CopyFileWithProgressAsync( sourceFile, destinationFile, progress );

                    // コピー成功後、元のファイルを削除
                    File.Delete( sourceFile );
                }
            } );

            // 全て終わったらアプリを終了
            Application.Current.Exit();
        }

        private async Task CopyFileWithProgressAsync( string source, string destination, IProgress<long> progress ) {
            const int bufferSize = 81920; // 80KB buffer
            long totalBytesCopied = 0;

            using ( var sourceStream = new FileStream( source, FileMode.Open, FileAccess.Read ) )
            using ( var destinationStream = new FileStream( destination, FileMode.Create, FileAccess.Write ) ) {
                TotalFileSize = sourceStream.Length;
                var buffer = new byte[bufferSize];
                int bytesRead;
                while ( (bytesRead = await sourceStream.ReadAsync( buffer, 0, buffer.Length )) > 0 ) {
                    await destinationStream.WriteAsync( buffer, 0, bytesRead );
                    totalBytesCopied += bytesRead;
                    progress.Report( totalBytesCopied );
                }
            }
        }
    }
}
