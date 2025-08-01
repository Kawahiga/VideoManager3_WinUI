using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace VideoManager3_WinUI
{
    public class DatabaseService
    {
        private readonly string _dbPath;

        public DatabaseService(string dbPath)
        {
            _dbPath = dbPath;
            InitializeDatabase();
        }

        private void InitializeDatabase()
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();

            var command = connection.CreateCommand();
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
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL UNIQUE
                );
                CREATE TABLE IF NOT EXISTS VideoTags (
                    VideoId INTEGER,
                    TagId INTEGER,
                    FOREIGN KEY (VideoId) REFERENCES Videos(FileID),
                    FOREIGN KEY (TagId) REFERENCES Tags(Id),
                    PRIMARY KEY (VideoId, TagId)
                );
            ";
            command.ExecuteNonQuery();
        }

        // 新しい動画をデータベースに追加する
        public async Task AddVideoAsync(VideoItem video)
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            // 既に存在する場合は無視する
            command.CommandText = @"
                INSERT OR IGNORE INTO Videos 
                (FilePath, FileName, FileSize, LastModified, Duration) 
                VALUES 
                ($filePath, $fileName, $fileSize, $lastModified, $duration)";
            command.Parameters.AddWithValue("$filePath", video.FilePath);
            command.Parameters.AddWithValue("$fileName", video.FileName);
            command.Parameters.AddWithValue("$fileSize", video.FileSize);
            command.Parameters.AddWithValue("$lastModified", video.LastModified);
            command.Parameters.AddWithValue("$duration", video.Duration);

            await command.ExecuteNonQueryAsync();
        }

        // データベースからすべての動画を読み込む
        public async Task<List<VideoItem>> GetAllVideosAsync()
        {
            var videos = new List<VideoItem>();
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT FileID, FilePath, FileName, FileSize, LastModified, Duration
                FROM Videos";

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var id = reader.GetInt32(0);
                var filePath = reader.GetString(1);
                var fileName = reader.GetString(2);
                var fileSize = reader.GetInt64(3);
                var lastModified = DateTime.Parse(reader.GetString(4));
                var duration = reader.GetDouble(5);

                var video = new VideoItem(id, filePath, fileName, fileSize, lastModified, duration);
                videos.Add(video);
            }
            return videos;
        }
    }
}
