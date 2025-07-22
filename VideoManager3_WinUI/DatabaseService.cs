using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace VideoManager3_WinUI
{
    public class DatabaseService
    {
        private readonly string _databasePath;

        public DatabaseService(string databasePath)
        {
            _databasePath = databasePath;
            InitializeDatabase();
        }

        private SqliteConnection GetConnection() => new SqliteConnection($"Data Source={_databasePath}");

        private void InitializeDatabase()
        {
            using (var connection = GetConnection())
            {
                connection.Open();
                var command = connection.CreateCommand();
                command.CommandText = "PRAGMA foreign_keys = ON;"; // 外部キー制約を有効にする
                command.ExecuteNonQuery();

                command.CommandText = @"
                    CREATE TABLE IF NOT EXISTS Videos (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT NOT NULL, Path TEXT NOT NULL UNIQUE,
                        Thumbnail BLOB, LastModified TEXT NOT NULL, Length TEXT, IsFolder BOOLEAN NOT NULL );";
                command.ExecuteNonQuery();
                command.CommandText = @"
                    CREATE TABLE IF NOT EXISTS Tags (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT NOT NULL, Color TEXT,
                        ParentId INTEGER, FOREIGN KEY (ParentId) REFERENCES Tags(Id) ON DELETE CASCADE );";
                command.ExecuteNonQuery();
                command.CommandText = @"
                    CREATE TABLE IF NOT EXISTS VideoTags (
                        VideoId INTEGER NOT NULL, TagId INTEGER NOT NULL, PRIMARY KEY (VideoId, TagId),
                        FOREIGN KEY (VideoId) REFERENCES Videos(Id) ON DELETE CASCADE,
                        FOREIGN KEY (TagId) REFERENCES Tags(Id) ON DELETE CASCADE );";
                command.ExecuteNonQuery();
            }
        }

        public void AddVideoItems(IEnumerable<VideoItem> videoItems)
        {
            using (var connection = GetConnection())
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    var command = connection.CreateCommand();
                    command.CommandText = @"INSERT OR IGNORE INTO Videos (Name, Path, LastModified, IsFolder)
                                            VALUES ($Name, $Path, $LastModified, $IsFolder);";
                    foreach (var item in videoItems)
                    {
                        command.Parameters.Clear();
                        command.Parameters.AddWithValue("$Name", item.Name);
                        command.Parameters.AddWithValue("$Path", item.Path);
                        command.Parameters.AddWithValue("$LastModified", item.LastModified.ToString("o"));
                        command.Parameters.AddWithValue("$IsFolder", item.IsFolder);
                        command.ExecuteNonQuery();
                    }
                    transaction.Commit();
                }
            }
        }
        
        // 新しいタグをDBに追加する
        public void AddTag(string name, int? parentId)
        {
            using (var connection = GetConnection())
            {
                connection.Open();
                var command = connection.CreateCommand();
                command.CommandText = "INSERT INTO Tags (Name, ParentId) VALUES ($Name, $ParentId);";
                command.Parameters.AddWithValue("$Name", name);
                command.Parameters.AddWithValue("$ParentId", parentId.HasValue ? (object)parentId.Value : DBNull.Value);
                command.ExecuteNonQuery();
            }
        }

        public ObservableCollection<VideoItem> GetVideos()
        {
            var videos = new ObservableCollection<VideoItem>();
            using (var connection = GetConnection())
            {
                connection.Open();
                var command = connection.CreateCommand();
                command.CommandText = "SELECT Id, Name, Path, LastModified, IsFolder FROM Videos";
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        videos.Add(new VideoItem { Id = reader.GetInt32(0), Name = reader.GetString(1), Path = reader.GetString(2), LastModified = DateTime.Parse(reader.GetString(3)), IsFolder = reader.GetBoolean(4) });
                    }
                }
            }
            return videos;
        }

        // DBからすべてのタグを取得し、階層構造を構築して返す
        public ObservableCollection<TagItem> GetTags()
        {
            var tags = new List<TagItem>();
            using (var connection = GetConnection())
            {
                connection.Open();
                var command = connection.CreateCommand();
                command.CommandText = "SELECT Id, Name, Color, ParentId FROM Tags";
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        tags.Add(new TagItem
                        {
                            Id = reader.GetInt32(0),
                            Name = reader.GetString(1),
                            Color = reader.IsDBNull(2) ? null : reader.GetString(2),
                            ParentId = reader.IsDBNull(3) ? null : (int?)reader.GetInt32(3)
                        });
                    }
                }
            }

            // 階層構造を構築
            var tagDictionary = tags.ToDictionary(t => t.Id);
            var rootTags = new ObservableCollection<TagItem>();
            foreach (var tag in tags)
            {
                if (tag.ParentId.HasValue && tagDictionary.TryGetValue(tag.ParentId.Value, out var parent))
                {
                    parent.Children.Add(tag);
                }
                else
                {
                    rootTags.Add(tag);
                }
            }
            return rootTags;
        }
    }
}