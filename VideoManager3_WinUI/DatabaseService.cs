using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

// 未実装項目
// ・動画をデータベースから削除
// ・タグをデータベースから削除
// ・動画からタグを削除
// ・例外処理の実装
// ・タグから動画を取得（タグからの絞り込みで必要？）
// ・リンク切れになった動画を探して削除
// そもそも起動時のDBアクセスは1つのメソッドにまとめた方がいい？

namespace VideoManager3_WinUI {
    public class DatabaseService {
        private readonly string _dbPath;

        public DatabaseService( string dbPath ) {
            _dbPath = dbPath;
            InitializeDatabase();
        }

        private void InitializeDatabase() {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();

            var command = connection.CreateCommand();

            // DROP TABLE IF EXISTS Tags;
            command.CommandText =
            @"
                CREATE TABLE IF NOT EXISTS Videos (
                    FileID INTEGER PRIMARY KEY AUTOINCREMENT,
                    FilePath TEXT NOT NULL UNIQUE,
                    FileName TEXT NOT NULL,
                    FileSize INTEGER NOT NULL,
                    LastModified TEXT NOT NULL,
                    Duration REAL NOT NULL
                );
                
                CREATE TABLE IF NOT EXISTS Tags (
                    TagID INTEGER PRIMARY KEY AUTOINCREMENT,
                    TagName TEXT NOT NULL UNIQUE,
                    TagColor TEXT,
                    Parent INTEGER,
                    OrderInGroup INTEGER DEFAULT 0,
                    IsGroup BOOLEAN DEFAULT 0,
                    FOREIGN KEY (Parent) REFERENCES Tags(TagID)
                );
                
                CREATE TABLE IF NOT EXISTS VideoTags (
                    VideoId INTEGER,
                    TagId INTEGER,
                    FOREIGN KEY (VideoId) REFERENCES Videos(FileID),
                    FOREIGN KEY (TagId) REFERENCES Tags(TagID),
                    PRIMARY KEY (VideoId, TagId)
                );
            ";
            // エンハンス案：関連づいたIDが削除された場合に、関連する行も削除するためのON DELETE CASCADEを設定
            // FOREIGN KEY (VideoId) REFERENCES Videos(FileID) ON DELETE CASCADE,
            // FOREIGN KEY(TagId) REFERENCES Tags(TagID) ON DELETE CASCADE,

            command.ExecuteNonQuery();
        }

        // 新しい動画をデータベースに追加する
        public async Task AddVideoAsync( VideoItem video ) {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            // 既に存在する場合は無視する
            command.CommandText = @"
                INSERT OR IGNORE INTO Videos 
                (FilePath, FileName, FileSize, LastModified, Duration) 
                VALUES 
                ($filePath, $fileName, $fileSize, $lastModified, $duration)";
            command.Parameters.AddWithValue( "$filePath", video.FilePath );
            command.Parameters.AddWithValue( "$fileName", video.FileName );
            command.Parameters.AddWithValue( "$fileSize", video.FileSize );
            //command.Parameters.AddWithValue("$lastModified", video.LastModified);
            // 日付は環境に依存しないISO 8601形式("o")で保存する
            command.Parameters.AddWithValue( "$lastModified", video.LastModified.ToString( "o" ) );
            command.Parameters.AddWithValue( "$duration", video.Duration );

            // IDを設定
            var rowsAffected = await command.ExecuteNonQueryAsync();

            // INSERT OR IGNORE を使っているため、行が実際に挿入されたか確認が必要です。
            // 挿入された場合(rowsAffected > 0)、新しく生成されたIDを取得してVideoItemに設定します。
            if ( rowsAffected > 0 ) {
                command.CommandText = "SELECT last_insert_rowid()";
                command.Parameters.Clear();
                video.Id = Convert.ToInt32( await command.ExecuteScalarAsync() );
            } else {
                // 挿入されなかった場合（既に存在していた場合）、既存のIDを取得します。
                command.CommandText = "SELECT FileID FROM Videos WHERE FilePath = $filePath";
                command.Parameters.Clear();
                command.Parameters.AddWithValue( "$filePath", video.FilePath );
                video.Id = Convert.ToInt32( await command.ExecuteScalarAsync() );
            }
        }

        // データベースからすべての動画を読み込む
        public async Task<List<VideoItem>> GetAllVideosAsync() {
            var videos = new List<VideoItem>();
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT FileID, FilePath, FileName, FileSize, LastModified, Duration
                FROM Videos";

            using var reader = await command.ExecuteReaderAsync();
            while ( await reader.ReadAsync() ) {
                var id = reader.GetInt32(0);
                var filePath = reader.GetString(1);
                var fileName = reader.GetString(2);
                var fileSize = reader.GetInt64(3);
                // ISO 8601形式で保存した日付を正しく読み込むため、スタイルを指定します
                var lastModified = DateTime.Parse(reader.GetString(4), null, System.Globalization.DateTimeStyles.RoundtripKind);
                var duration = reader.GetDouble(5);

                var video = new VideoItem(id, filePath, fileName, fileSize, lastModified, duration);
                videos.Add( video );
            }
            return videos;
        }

        // タグをデータベースに追加または更新する
        public async Task AddOrUpdateTagAsync( TagItem tag ) {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            await connection.OpenAsync();
            var command = connection.CreateCommand();

            // parentの値が0の場合はnullを設定
            if ( tag.ParentId == 0 )
                tag.ParentId = null;

            if ( tag.Id == 0 ) {
                // 新規追加
                command.CommandText = @"
                    INSERT INTO Tags (TagName, TagColor, Parent, OrderInGroup, IsGroup)
                    VALUES ($name, $color, $parent, $order, $isGroup);
                ";
                command.Parameters.AddWithValue( "$name", tag.Name );
                command.Parameters.AddWithValue( "$color", tag.ColorCode ?? (object)DBNull.Value );
                command.Parameters.AddWithValue( "$parent", tag.ParentId ?? (object)DBNull.Value );
                command.Parameters.AddWithValue( "$order", tag.OrderInGroup );
                command.Parameters.AddWithValue( "$isGroup", tag.IsGroup ? 1 : 0 );
                await command.ExecuteNonQueryAsync();

                // 新しく生成されたIDを取得してセット
                command.CommandText = "SELECT last_insert_rowid()";
                command.Parameters.Clear();
                tag.Id = Convert.ToInt32( await command.ExecuteScalarAsync() );
            } else {
                // 更新
                command.CommandText = @"
                    UPDATE Tags SET
                        TagName = $name,
                        TagColor = $color,
                        Parent = $parent,
                        OrderInGroup = $order,
                        IsGroup = $isGroup
                    WHERE TagID = $id;
                ";
                command.Parameters.AddWithValue( "$id", tag.Id );
                command.Parameters.AddWithValue( "$name", tag.Name );
                command.Parameters.AddWithValue( "$color", tag.ColorCode ?? (object)DBNull.Value );
                command.Parameters.AddWithValue( "$parent", tag.ParentId ?? (object)DBNull.Value );
                command.Parameters.AddWithValue( "$order", tag.OrderInGroup );
                command.Parameters.AddWithValue( "$isGroup", tag.IsGroup ? 1 : 0 );
                await command.ExecuteNonQueryAsync();
            }
        }

        // データベースからすべてのタグを読み込む
        public async Task<List<TagItem>> GetTagsAsync() {
            var tags = new List<TagItem>();
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT TagID, TagName, TagColor, Parent, OrderInGroup, IsGroup
                FROM Tags";

            using var reader = await command.ExecuteReaderAsync();
            while ( await reader.ReadAsync() ) {
                var id = reader.GetInt32(0);
                var name = reader.GetString(1);
                var color = reader.IsDBNull(2) ? "#000000" : reader.GetString(2); // DBのColorがNULLの場合、黒をデフォルト値とする
                var parent = reader.IsDBNull(3) ? 0 : reader.GetInt32(3);   // DBのParentIdがNULLの場合、0をデフォルト値とする
                var orderInGroup = reader.GetInt32(4);
                var isGroup = reader.GetBoolean(5);

                var tag = new TagItem
                {
                    Id = id,
                    Name = name,
                    ColorCode = color,
                    ParentId = parent,
                    OrderInGroup = orderInGroup,
                    IsGroup = isGroup
                };
                tags.Add( tag );

            }
            return tags;
        }

        // 動画にタグを追加する
        public async Task AddTagToVideoAsync( VideoItem video, TagItem tag ) {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            await connection.OpenAsync();
            var command = connection.CreateCommand();

            // 既に存在する場合は無視する
            command.CommandText = @"
                INSERT OR IGNORE INTO VideoTags (VideoId, TagId)
                VALUES ($videoId, $tagId);
            ";
            command.Parameters.AddWithValue( "$videoId", video.Id );
            command.Parameters.AddWithValue( "$tagId", tag.Id );
            await command.ExecuteNonQueryAsync();
        }
        // 動画からタグを削除する
        public async Task RemoveTagFromVideoAsync( VideoItem video, TagItem tag ) {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            await connection.OpenAsync();
            var command = connection.CreateCommand();
            command.CommandText = @"
                DELETE FROM VideoTags
                WHERE VideoId = $videoId AND TagId = $tagId;
            ";
            command.Parameters.AddWithValue( "$videoId", video.Id );
            command.Parameters.AddWithValue( "$tagId", tag.Id );
            await command.ExecuteNonQueryAsync();
        }

        // データベースから動画に関連付けられたタグを取得する
        public async Task<List<TagItem>> GetTagsForVideoAsync( VideoItem video ) {
            var tags = new List<TagItem>();
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            await connection.OpenAsync();
            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT t.TagID, t.TagName, t.TagColor, t.Parent, t.OrderInGroup, t.IsGroup
                FROM Tags t
                JOIN VideoTags vt ON vt.TagId = t.TagID
                WHERE vt.VideoId = $videoId;
            ";
            command.Parameters.AddWithValue( "$videoId", video.Id );
            using var reader = await command.ExecuteReaderAsync();
            while ( await reader.ReadAsync() ) {
                var id = reader.GetInt32(0);
                var name = reader.GetString(1);
                var color = reader.IsDBNull(2) ? "#000000" : reader.GetString(2); // DBのColorがNULLの場合、黒をデフォルト値とする
                var parent = reader.IsDBNull(3) ? 0 : reader.GetInt32(3);   // DBのParentIdがNULLの場合、0をデフォルト値とする
                var orderInGroup = reader.GetInt32(4);
                var isGroup = reader.GetBoolean(5);
                var tag = new TagItem
                {
                    Id = id,
                    Name = name,
                    ColorCode = color,
                    ParentId = parent,
                    OrderInGroup = orderInGroup,
                    IsGroup = isGroup
                };
                tags.Add( tag );
            }
            return tags;
        }
    }
}
