using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VideoManager3_WinUI.Models;

// 未実装項目
// ・動画をデータベースから削除
// ・タグをデータベースから削除
// ・動画からタグを削除
// ・例外処理の実装
// ・タグから動画を取得（タグからの絞り込みで必要？）
// ・リンク切れになった動画を探して削除
// ・タググループ削除時に配下も削除
// ・Transaction CommitAsync RollbackAsyncの仕組み
// そもそも起動時のDBアクセスは1つのメソッドにまとめた方がいい？
//
// DBにゴミが溜まるケース
// ・ファイル名の変更により、アーティストとその関連付けが残ったままになる
// ・ファイルへのリンク切れにより、動画とその関連付けが残ったままになる

namespace VideoManager3_WinUI.Services {
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
                    FenrirFileID INTEGER DEFAULT 0,
                    FilePath TEXT NOT NULL UNIQUE,
                    FileName TEXT NOT NULL,
                    FileNameWithoutArtist TEXT NOT NULL,
                    Extension TEXT DEFAULT '',
                    FileSize INTEGER DEFAULT 0,
                    LastModified TEXT,
                    Duration REAL DeFAULT 0.0,
                    LikeCount INTEGER DEFAULT 0,
                    ViewCount INTEGER DEFAULT 0
                );
                
                CREATE TABLE IF NOT EXISTS Tags (
                    TagID INTEGER PRIMARY KEY AUTOINCREMENT,
                    TagName TEXT NOT NULL UNIQUE,
                    TagColor TEXT,
                    Parent INTEGER,
                    OrderInGroup INTEGER DEFAULT 0,
                    IsGroup BOOLEAN DEFAULT 0,
                    IsExpand BOOLEAN DEFAULT 1,
                    FOREIGN KEY (Parent) REFERENCES Tags(TagID)
                );
                
                CREATE TABLE IF NOT EXISTS VideoTags (
                    VideoId INTEGER,
                    TagId INTEGER,
                    FOREIGN KEY (VideoId) REFERENCES Videos(FileID),
                    FOREIGN KEY (TagId) REFERENCES Tags(TagID),
                    PRIMARY KEY (VideoId, TagId)
                );

                CREATE TABLE IF NOT EXISTS Artists (
                    ArtistID INTEGER PRIMARY KEY AUTOINCREMENT,
                    ArtistName TEXT NOT NULL,
                    IsFavorite BOOLEAN DEFAULT 0,
                    LikeCount INTEGER DEFAULT 0,
                    IconPath TEXT
                );

                CREATE TABLE IF NOT EXISTS VideoArtists (
                    VideoId INTEGER NOT NULL,
                    ArtistID INTEGER NOT NULL,
                    PRIMARY KEY (VideoId, ArtistID),
                    FOREIGN KEY (VideoId) REFERENCES Videos(FileID),
                    FOREIGN KEY (ArtistID) REFERENCES Artists(ArtistID)
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
                    (FilePath, FileName, FileNameWithoutArtist, Extension, FileSize, LastModified, Duration, LikeCount, ViewCount) 
                    VALUES 
                    ($filePath, $fileName, $fileNameWithiutArtist, $extension, $fileSize, $lastModified, $duration, $like, $view)";
            command.Parameters.AddWithValue( "$filePath", video.FilePath );
            command.Parameters.AddWithValue( "$fileName", video.FileName );
            command.Parameters.AddWithValue( "$fileNameWithiutArtist", video.FileNameWithoutArtists );
            command.Parameters.AddWithValue( "$extension", video.Extension ?? string.Empty ); // 拡張子がnullの場合は空文字列を設定
            command.Parameters.AddWithValue( "$fileSize", video.FileSize );
            command.Parameters.AddWithValue( "$lastModified", video.LastModified.ToString( "o" ) ); // 日付は環境に依存しないISO 8601形式("o")で保存する
            command.Parameters.AddWithValue( "$duration", video.Duration );
            command.Parameters.AddWithValue( "$like", video.LikeCount );
            command.Parameters.AddWithValue( "$view", video.ViewCount );

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

        // 動画のデータを更新する
        public async Task UpdateVideoAsync( VideoItem video ) {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE Videos SET
                    FenrirFileID = $fenrirId,
                    FilePath = $filePath,
                    FileName = $fileName,
                    FileNameWithoutArtist = $fileNameWithiutArtist,
                    Extension = $extension,
                    FileSize = $fileSize,
                    LastModified = $lastModified,
                    Duration = $duration,
                    LikeCount = $like,
                    ViewCount = $view
                WHERE FileID = $id;";
            command.Parameters.AddWithValue( "$fenrirId", video.FenrirId );
            command.Parameters.AddWithValue( "$filePath", video.FilePath );
            command.Parameters.AddWithValue( "$fileName", video.FileName );
            command.Parameters.AddWithValue( "$fileNameWithiutArtist", video.FileNameWithoutArtists );
            command.Parameters.AddWithValue( "$extension", video.Extension ?? string.Empty );
            command.Parameters.AddWithValue( "$fileSize", video.FileSize );
            command.Parameters.AddWithValue( "$lastModified", video.LastModified.ToString( "o" ) );
            command.Parameters.AddWithValue( "$duration", video.Duration );
            command.Parameters.AddWithValue( "$like", video.LikeCount );
            command.Parameters.AddWithValue( "$view", video.ViewCount );
            command.Parameters.AddWithValue( "$id", video.Id );

            await command.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// 動画をデータベースから削除する（タグ、アーティストとの関連付け情報も削除）
        /// </summary>
        /// <param name="video"></param>
        /// <returns></returns>
        public async Task DeleteVideoAsync( VideoItem video ) {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            await connection.OpenAsync();
            var command = connection.CreateCommand();

            // VideoTagsテーブルから関連付けを削除
            command.CommandText = "DELETE FROM VideoTags WHERE VideoId = $id;";
            command.Parameters.AddWithValue( "$id", video.Id );
            await command.ExecuteNonQueryAsync();

            // VideoArtistsテーブルから関連付けを削除
            command.CommandText = "DELETE FROM VideoArtists WHERE VideoId = $id;";
            await command.ExecuteNonQueryAsync();

            // Videosテーブルから動画を削除
            command.CommandText = "DELETE FROM Videos WHERE FileID = $id;";
            await command.ExecuteNonQueryAsync();
        }

        // データベースからすべての動画を読み込む
        public async Task<List<VideoItem>> GetAllVideosAsync() {
            var videos = new List<VideoItem>();
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT FileID, FenrirFileID, FilePath, FileName, FileNameWithoutArtist, Extension, FileSize, LastModified, Duration, LikeCount, ViewCount
                FROM Videos";

            using var reader = await command.ExecuteReaderAsync();
            while ( await reader.ReadAsync() ) {
                var id = reader.GetInt32(0);
                var fenrirId = reader.GetInt32(1);
                var filePath = reader.GetString(2);
                var fileName = reader.GetString(3);
                var fileNameWithoutArtist = reader.GetString(4);
                var extension = reader.IsDBNull(5) ? string.Empty : reader.GetString(5); // 拡張子がNULLの場合は空文字列を設定
                var fileSize = reader.GetInt64(6);
                // ISO 8601形式で保存した日付を正しく読み込むため、スタイルを指定します
                var lastModified = DateTime.Parse(reader.GetString(7), null, System.Globalization.DateTimeStyles.RoundtripKind);
                var duration = reader.GetDouble(8);
                var likeCount = reader.GetInt32(9);
                var viewCount = reader.GetInt32(10);

                var video = new VideoItem {
                    Id = id,
                    FenrirId = fenrirId,
                    FilePath = filePath,
                    FileName = fileName,
                    FileNameWithoutArtists = fileNameWithoutArtist,
                    Extension = extension,
                    FileSize = fileSize,
                    LastModified = lastModified,
                    Duration = duration,
                    LikeCount = likeCount,
                    ViewCount = viewCount
                 };
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
                    INSERT INTO Tags (TagName, TagColor, Parent, OrderInGroup, IsGroup, IsExpand)
                    VALUES ($name, $color, $parent, $order, $isGroup, $isExpand);
                ";
                command.Parameters.AddWithValue( "$name", tag.Name );
                command.Parameters.AddWithValue( "$color", tag.ColorCode ?? (object)DBNull.Value );
                command.Parameters.AddWithValue( "$parent", tag.ParentId ?? (object)DBNull.Value );
                command.Parameters.AddWithValue( "$order", tag.OrderInGroup );
                command.Parameters.AddWithValue( "$isGroup", tag.IsGroup ? 1 : 0 );
                command.Parameters.AddWithValue( "$isExpand", tag.IsExpanded ? 1 : 0 );
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
                        IsGroup = $isGroup,
                        IsExpand = $isExpand
                    WHERE TagID = $id;
                ";
                command.Parameters.AddWithValue( "$id", tag.Id );
                command.Parameters.AddWithValue( "$name", tag.Name );
                command.Parameters.AddWithValue( "$color", tag.ColorCode ?? (object)DBNull.Value );
                command.Parameters.AddWithValue( "$parent", tag.ParentId ?? (object)DBNull.Value );
                command.Parameters.AddWithValue( "$order", tag.OrderInGroup );
                command.Parameters.AddWithValue( "$isGroup", tag.IsGroup ? 1 : 0 );
                command.Parameters.AddWithValue( "$isExpand", tag.IsExpanded ? 1 : 0 );
                await command.ExecuteNonQueryAsync();
            }
        }

        // タグをデータベースから削除する
        public async Task DeleteTagAsync( TagItem tag ) {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            await connection.OpenAsync();
            var command = connection.CreateCommand();

            // VideoTagsテーブルから関連付けを削除
            command.CommandText = "DELETE FROM VideoTags WHERE TagId = $id;";
            command.Parameters.AddWithValue( "$id", tag.Id );
            await command.ExecuteNonQueryAsync();

            // Tagsテーブルからタグを削除
            command.CommandText = "DELETE FROM Tags WHERE TagID = $id;";
            await command.ExecuteNonQueryAsync();
        }

        // データベースからすべてのタグを読み込む
        public async Task<List<TagItem>> GetTagsAsync() {
            var tags = new List<TagItem>();
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT TagID, TagName, TagColor, Parent, OrderInGroup, IsGroup, IsExpand
                FROM Tags";

            using var reader = await command.ExecuteReaderAsync();
            while ( await reader.ReadAsync() ) {
                var id = reader.GetInt32(0);
                var name = reader.GetString(1);
                var color = reader.IsDBNull(2) ? "#000000" : reader.GetString(2); // DBのColorがNULLの場合、黒をデフォルト値とする
                var parent = reader.IsDBNull(3) ? 0 : reader.GetInt32(3);   // DBのParentIdがNULLの場合、0をデフォルト値とする
                var orderInGroup = reader.GetInt32(4);
                var isGroup = reader.GetBoolean(5);
                var isExpanded = reader.GetBoolean(6);

                var tag = new TagItem
                {
                    Id = id,
                    Name = name,
                    ColorCode = color,
                    ParentId = parent,
                    OrderInGroup = orderInGroup,
                    IsGroup = isGroup,
                    IsExpanded = isExpanded
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
                SELECT t.TagID, t.TagName, t.TagColor, t.Parent, t.OrderInGroup, t.IsGroup, t.IsExpand
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
                var isExpanded = reader.GetBoolean(6);
                var tag = new TagItem
                {
                    Id = id,
                    Name = name,
                    ColorCode = color,
                    ParentId = parent,
                    OrderInGroup = orderInGroup,
                    IsGroup = isGroup,
                    IsExpanded = isExpanded
                };
                tags.Add( tag );
            }
            return tags;
        }

    /* タグに関する処理↑   ↓アーティストに関する処理 */

        // アーティストをデータベースに追加または更新する
        public async Task AddOrUpdateArtistAsync( ArtistItem artist ) {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            await connection.OpenAsync();
            var command = connection.CreateCommand();
            if ( artist.Id == 0 ) {
                // 新規追加
                command.CommandText = @"
                    INSERT INTO Artists (ArtistName, IsFavorite, LikeCount, IconPath) 
                    ($artistName, $isFavorite, $likeCount, $iconPath)
                ";
                command.Parameters.AddWithValue( "$name", artist.Name );
                command.Parameters.AddWithValue( "$isFavorite", artist.IsFavorite ? 1 : 0 );
                command.Parameters.AddWithValue( "$likeCount", artist.LikeCount );
                command.Parameters.AddWithValue( "$iconPath", artist.IconPath ?? (object)DBNull.Value );
                await command.ExecuteNonQueryAsync();
                // 新しく生成されたIDを取得してセット
                command.CommandText = "SELECT last_insert_rowid()";
                command.Parameters.Clear();
                artist.Id = Convert.ToInt32( await command.ExecuteScalarAsync() );
            } else {
                // 更新
                command.CommandText = @"
                    UPDATE Artists SET
                        ArtistName = $name,
                        IsFavorite = $isFavorite,
                        LikeCount = $likeCount,
                        IconPath = $iconPath
                    WHERE ArtistID = $id;
                ";
                command.Parameters.AddWithValue( "$id", artist.Id );
                command.Parameters.AddWithValue( "$name", artist.Name );
                command.Parameters.AddWithValue( "$isFavorite", artist.IsFavorite ? 1 : 0 );
                command.Parameters.AddWithValue( "$likeCount", artist.LikeCount );
                command.Parameters.AddWithValue( "$iconPath", artist.IconPath ?? (object)DBNull.Value );
                await command.ExecuteNonQueryAsync();
            }
        }

        /// <summary>
        /// アーティストをデータベースから削除する（動画との関連付け情報も削除）  
        /// </summary>
        public async Task DeleteArtistAsync( ArtistItem artist ) {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            await connection.OpenAsync();
            var command = connection.CreateCommand();

            // VideoArtistsテーブルから関連付けを削除
            command.CommandText = "DELETE FROM VideoArtists WHERE ArtistID = $id;";
            command.Parameters.AddWithValue( "$id", artist.Id );
            await command.ExecuteNonQueryAsync();

            // Artistsテーブルからアーティストを削除
            command.CommandText = "DELETE FROM Artists WHERE ArtistID = $id;";
            await command.ExecuteNonQueryAsync();
        }

        // データベースからすべてのアーティストを読み込む
        public async Task<List<ArtistItem>> GetAllArtistsAsync() {
            var artists = new List<ArtistItem>();
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            await connection.OpenAsync();
            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT ArtistID, ArtistName, IsFavorite, LikeCount, IconPath
                FROM Artists";
            using var reader = await command.ExecuteReaderAsync();
            while ( await reader.ReadAsync() ) {
                var id = reader.GetInt32(0);
                var name = reader.GetString(1);
                var isFavorite = reader.GetBoolean(2);
                var likeCount = reader.GetInt32(3);
                var iconPath = reader.IsDBNull(4) ? string.Empty : reader.GetString(3);
                var artist = new ArtistItem {
                    Id = id,
                    Name = name,
                    IsFavorite = isFavorite,
                    LikeCount = likeCount,
                    IconPath = iconPath
                };
                artists.Add( artist );
            }
            return artists;
        }

        // 動画にアーティストを追加する
        public async Task AddArtistToVideoAsync( VideoItem video, ArtistItem artist ) {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            await connection.OpenAsync();
            var command = connection.CreateCommand();
            // 既に存在する場合は無視する
            command.CommandText = @"
                INSERT OR IGNORE INTO VideoArtists (VideoId, ArtistID)
                VALUES ($videoId, $artistId);
            ";
            command.Parameters.AddWithValue( "$videoId", video.Id );
            command.Parameters.AddWithValue( "$artistId", artist.Id );
            await command.ExecuteNonQueryAsync();
        }

        // 動画からアーティストを削除する
        public async Task RemoveArtistFromVideoAsync( VideoItem video, ArtistItem artist ) {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            await connection.OpenAsync();
            var command = connection.CreateCommand();
            command.CommandText = @"
                DELETE FROM VideoArtists
                WHERE VideoId = $videoId AND ArtistID = $artistId;
            ";
            command.Parameters.AddWithValue( "$videoId", video.Id );
            command.Parameters.AddWithValue( "$artistId", artist.Id );
            await command.ExecuteNonQueryAsync();
        }

        // データベースからアーティストと動画の関連情報を取得する
        public async Task<List<ArtistItem>> GetArtistsForVideoAsync( VideoItem video ) {
            var artists = new List<ArtistItem>();
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            await connection.OpenAsync();
            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT a.ArtistID, a.ArtistName, a.IsFavorite, a.IconPath
                FROM Artists a
                JOIN VideoArtists va ON va.ArtistID = a.ArtistID
                WHERE va.VideoId = $videoId;
            ";
            command.Parameters.AddWithValue( "$videoId", video.Id );
            using var reader = await command.ExecuteReaderAsync();
            while ( await reader.ReadAsync() ) {
                var id = reader.GetInt32(0);
                var name = reader.GetString(1);
                var isFavorite = reader.GetBoolean(2);
                var iconPath = reader.IsDBNull(3) ? string.Empty : reader.GetString(3);
                var artist = new ArtistItem {
                    Id = id,
                    Name = name,
                    IsFavorite = isFavorite,
                    IconPath = iconPath
                };
                artists.Add( artist );
            }
            return artists;
        }
    }
}
