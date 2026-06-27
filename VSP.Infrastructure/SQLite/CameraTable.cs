using Microsoft.Data.Sqlite;

namespace VSP.Infrastructure.SQLite;

public static class CameraTable
{
    public static void Create(SqliteConnection connection)
    {
        const string sql = @"
CREATE TABLE IF NOT EXISTS Camera
(
    Id TEXT PRIMARY KEY,
    Name TEXT NOT NULL,
    IpAddress TEXT NOT NULL,
    Brand INTEGER NOT NULL,

    Model TEXT,
    Location TEXT,

    HttpPort INTEGER,
    RtspPort INTEGER,
    SdkPort INTEGER,

    Username TEXT,
    Password TEXT,
    RtspUrl TEXT,

    Status INTEGER,
    Recording INTEGER,

    CreateTime TEXT,
    LastModifyTime TEXT
);";

        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}