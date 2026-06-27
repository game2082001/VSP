using VSP.Infrastructure.SQLite;

namespace VSP.Infrastructure.Database;

public class DatabaseInitializer
{
    private readonly DatabaseService _databaseService;

    public DatabaseInitializer(DatabaseService databaseService)
    {
        _databaseService = databaseService;
    }

    public void Initialize()
    {
        using var connection = _databaseService.CreateConnection();

        connection.Open();

        CameraTable.Create(connection);
    }
}