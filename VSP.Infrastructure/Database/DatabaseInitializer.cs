using VSP.Infrastructure.SQLite;

namespace VSP.Infrastructure.Database;

/// <summary>
/// Deliberately does not log its own failure (unlike the other five Epic-015 components) --
/// the caller generates the single Error ID for a startup failure and logs the original
/// exception together with that ID in one line, so the ID and the exception are never split
/// across two separate log entries. See VSP.UI/App.xaml.cs's HandleDatabaseInitializationFailure.
/// </summary>
public class DatabaseInitializer
{
    private readonly DatabaseService _databaseService;

    public DatabaseInitializer(DatabaseService databaseService)
    {
        _databaseService = databaseService;
    }

    public DatabaseInitializationResult Initialize()
    {
        try
        {
            using var connection = _databaseService.CreateConnection();

            connection.Open();

            CameraTable.Create(connection);
            UserTable.Create(connection);
            DefaultAdminSeeder.SeedIfEmpty(connection);

            return DatabaseInitializationResult.Ok();
        }
        catch (Exception ex)
        {
            return DatabaseInitializationResult.Failed(ex);
        }
    }
}