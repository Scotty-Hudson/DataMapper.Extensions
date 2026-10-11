using Microsoft.Data.Sqlite;

namespace DataMapper.Extensions.Web.Data
{
    public interface IDatabase
    {
        string Path { get; }

        /// <summary>
        /// A new connection. The library does not own connection lifetimes, so this is the
        /// caller's job: open, use, dispose.
        /// </summary>
        SqliteConnection Open();
    }

    public sealed class SqliteDatabase : IDatabase
    {
        private readonly string _connectionString;

        public SqliteDatabase(string path)
        {
            Path = path;

            var directory = System.IO.Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            _connectionString = $"Data Source={path}";
        }

        public string Path { get; }

        public SqliteConnection Open()
        {
            var connection = new SqliteConnection(_connectionString);
            connection.Open();
            return connection;
        }
    }
}
