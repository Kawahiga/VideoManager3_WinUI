using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VideoManager3_WinUI.Models;

namespace VideoManager3_WinUI.Services {

    // Fenrir用のデータベースサービス
    // 暫定対応として実装
    // ファイルへのタグ付け/タグ解除のみ実装
    public class DatabaseServiceForFenrir {
        private readonly string _dbPath;

        public DatabaseServiceForFenrir( string dbPath ) {
            _dbPath = dbPath;
        }

        // ファイルにタグを追加する
        public async Task AddTagToVideoAsync( VideoItem video, TagItem tag ) {
            try {
                using var connection = new SqliteConnection($"Data Source={_dbPath}");
                await connection.OpenAsync();
                var command = connection.CreateCommand();

                // 既に存在する場合は無視する
                command.CommandText = @"
                INSERT OR IGNORE INTO labeledfiles (LabelID, FileId)
                VALUES ($labelId, $fileId);
            ";
                command.Parameters.AddWithValue( "$labelId", tag.Id );
                command.Parameters.AddWithValue( "$fileId", video.FenrirId );
                await command.ExecuteNonQueryAsync();

            } catch ( Exception ex ) {
                Console.WriteLine( $"Error fenrir database: {ex.Message}" );
            }
        }

        // 動画からタグを削除する
        public async Task RemoveTagFromVideoAsync( VideoItem video, TagItem tag ) {
            try {
                using var connection = new SqliteConnection($"Data Source={_dbPath}");
                await connection.OpenAsync();
                var command = connection.CreateCommand();
                command.CommandText = @"
                DELETE FROM labeledfiles
                WHERE FileId = $videoId AND LabelID = $tagId;
            ";
                command.Parameters.AddWithValue( "$videoId", video.FenrirId );
                command.Parameters.AddWithValue( "$tagId", tag.Id );
                await command.ExecuteNonQueryAsync();

            } catch ( Exception ex ) {
                Console.WriteLine( $"Error fenrir database: {ex.Message}" );
            }
        }
    }
}
