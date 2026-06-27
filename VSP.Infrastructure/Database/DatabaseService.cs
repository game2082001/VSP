using Microsoft.Data.Sqlite;
using System.Windows;

namespace VSP.Infrastructure.Database;

public class DatabaseService
{
    private static readonly string DatabaseFile =
        Path.Combine(AppContext.BaseDirectory, "vsp.db");

    public SqliteConnection CreateConnection()
    {
        
        return new SqliteConnection(
            $"Data Source={DatabaseFile}");
    }
}