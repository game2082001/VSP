using Microsoft.Data.Sqlite;

namespace VSP.Infrastructure.SQLite;

public static class UserTable
{
    public static void Create(SqliteConnection connection)
    {
        const string sql = @"
CREATE TABLE IF NOT EXISTS User
(
    Id TEXT PRIMARY KEY,
    Username TEXT NOT NULL,
    PasswordHash TEXT NOT NULL,
    PasswordSalt TEXT NOT NULL,
    PasswordIterations INTEGER NOT NULL,
    Role INTEGER NOT NULL,
    MustChangePassword INTEGER NOT NULL DEFAULT 0,

    CreateTime TEXT,
    LastModifyTime TEXT
);
CREATE UNIQUE INDEX IF NOT EXISTS IX_User_Username ON User (Username);";

        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
