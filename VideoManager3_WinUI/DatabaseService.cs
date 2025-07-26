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
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    FilePath TEXT NOT NULL UNIQUE,
                    FileName TEXT NOT NULL
                );
                CREATE TABLE IF NOT EXISTS Tags (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL UNIQUE
                );
                CREATE TABLE IF NOT EXISTS VideoTags (
                    VideoId INTEGER,
                    TagId INTEGER,
                    FOREIGN KEY (VideoId) REFERENCES Videos(Id),
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
            command.CommandText = "INSERT OR IGNORE INTO Videos (FilePath, FileName) VALUES ($filePath, $fileName)";
            command.Parameters.AddWithValue("$filePath", video.FilePath);
            command.Parameters.AddWithValue("$fileName", video.FileName);

            await command.ExecuteNonQueryAsync();
        }

        // データベースからすべての動画を読み込む
        public async Task<List<VideoItem>> GetAllVideosAsync()
        {
            var videos = new List<VideoItem>();
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT FilePath FROM Videos";

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                videos.Add(new VideoItem(reader.GetString(0)));
            }
            return videos;
        }
    }
}
