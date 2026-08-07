using Microsoft.Data.Sqlite;
using VSP.Core.Logging;
using VSP.Domain.Enums;
using VSP.Infrastructure.Database;
using VSP.Infrastructure.Repositories;
using VSP.Tests.Logging;
using Xunit;
using CameraEntity = VSP.Domain.Entities.Camera;

namespace VSP.Tests.Infrastructure;

[Collection("AppLog")]
public class DatabaseBackupServiceTests : IDisposable
{
    private readonly string _tempDirectory =
        Path.Combine(Path.GetTempPath(), $"vsp-db-backup-test-{Guid.NewGuid():N}");

    private DatabaseService CreateInitializedDatabase()
    {
        var databaseService = new DatabaseService(_tempDirectory);
        new DatabaseInitializer(databaseService).Initialize();
        return databaseService;
    }

    [Fact]
    public void Backup_ProducesValidFileWithSameData()
    {
        var databaseService = CreateInitializedDatabase();
        var repository = new SQLiteCameraRepository(databaseService);
        var camera = new CameraEntity { Name = "Front Door", IpAddress = "192.168.1.10", Brand = CameraBrand.Unknown };
        repository.Add(camera);

        var destinationPath = Path.Combine(_tempDirectory, "backup.db");
        var result = new DatabaseBackupService(databaseService).Backup(destinationPath);

        Assert.True(result.Success);
        Assert.Null(result.Exception);
        Assert.True(File.Exists(destinationPath));
        Assert.Equal(camera.Name, ReadSingleCameraName(destinationPath));
    }

    [Fact]
    public void Backup_ReflectsLatestWriteMadeImmediatelyBeforehand()
    {
        var databaseService = CreateInitializedDatabase();
        var repository = new SQLiteCameraRepository(databaseService);
        repository.Add(new CameraEntity { Name = "Stale", IpAddress = "192.168.1.10" });

        var latest = new CameraEntity { Name = "Latest", IpAddress = "192.168.1.11" };
        repository.Add(latest);

        var destinationPath = Path.Combine(_tempDirectory, "backup-latest.db");
        var result = new DatabaseBackupService(databaseService).Backup(destinationPath);

        Assert.True(result.Success);
        Assert.Contains("Latest", ReadAllCameraNames(destinationPath));
    }

    [Fact]
    public void Backup_WhenDestinationDirectoryDoesNotExist_ReturnsFailedResultWithOriginalException()
    {
        var databaseService = CreateInitializedDatabase();
        var destinationPath = Path.Combine(_tempDirectory, "missing-subdirectory", "backup.db");
        var recorder = new RecordingLogger();
        AppLog.Initialize(recorder);

        var result = new DatabaseBackupService(databaseService).Backup(destinationPath);

        Assert.False(result.Success);
        Assert.NotNull(result.Exception);
        var call = Assert.Single(recorder.Calls);
        Assert.Equal(LogLevel.Error, call.Level);
        Assert.NotNull(call.Exception);
    }

    private static string ReadSingleCameraName(string databasePath)
    {
        return Assert.Single(ReadAllCameraNames(databasePath));
    }

    private static List<string> ReadAllCameraNames(string databasePath)
    {
        using var connection = new SqliteConnection($"Data Source={databasePath};Mode=ReadOnly");
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Name FROM Camera;";
        using var reader = command.ExecuteReader();

        var names = new List<string>();
        while (reader.Read())
        {
            names.Add(reader.GetString(0));
        }

        return names;
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }
        catch
        {
            // best-effort cleanup
        }
    }
}
