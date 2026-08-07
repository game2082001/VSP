using Microsoft.Data.Sqlite;
using System.Windows;

namespace VSP.Infrastructure.Database;

public class DatabaseService
{
    private static readonly string DefaultDatabaseDirectory =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VSP");

    private readonly string _databaseDirectory;
    private readonly string _databaseFile;

    public DatabaseService() : this(DefaultDatabaseDirectory)
    {
    }

    /// <summary>Test seam: resolves against an arbitrary directory instead of %LocalAppData%\VSP.</summary>
    internal DatabaseService(string databaseDirectory)
    {
        _databaseDirectory = databaseDirectory;
        _databaseFile = Path.Combine(_databaseDirectory, "vsp.db");
    }

    public SqliteConnection CreateConnection()
    {
        Directory.CreateDirectory(_databaseDirectory);

        return new SqliteConnection(
            $"Data Source={_databaseFile}");
    }

    /// <summary>Full path to the live database file (Epic-017) -- does not guarantee the file exists.</summary>
    public string GetDatabaseFilePath() => _databaseFile;

    /// <summary>Directory the live database file lives in (Epic-017) -- does not guarantee the directory exists.</summary>
    public string GetDatabaseDirectory() => _databaseDirectory;
}