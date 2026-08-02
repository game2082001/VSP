using VSP.Core.Logging;
using VSP.Domain.Enums;
using VSP.Infrastructure.Database;
using VSP.Infrastructure.Repositories;
using VSP.Tests.Logging;
using Xunit;
using CameraEntity = VSP.Domain.Entities.Camera;

namespace VSP.Tests.Infrastructure;

[Collection("AppLog")]
public class SQLiteCameraRepositoryTests : IDisposable
{
    private readonly string _tempDirectory =
        Path.Combine(Path.GetTempPath(), $"vsp-camera-repository-test-{Guid.NewGuid():N}");

    private SQLiteCameraRepository CreateRepository()
    {
        var databaseService = new DatabaseService(_tempDirectory);
        new DatabaseInitializer(databaseService).Initialize();
        return new SQLiteCameraRepository(databaseService);
    }

    [Fact]
    public void AddThenGetAll_RoundTripsCamera()
    {
        var repository = CreateRepository();
        var camera = new CameraEntity { Name = "Front Door", IpAddress = "192.168.1.10", Brand = CameraBrand.Unknown };

        repository.Add(camera);
        var all = repository.GetAll();

        var stored = Assert.Single(all);
        Assert.Equal(camera.Id, stored.Id);
        Assert.Equal("Front Door", stored.Name);
    }

    [Fact]
    public void Update_ThenGetAll_ReflectsChange()
    {
        var repository = CreateRepository();
        var camera = new CameraEntity { Name = "Original", IpAddress = "192.168.1.10" };
        repository.Add(camera);

        camera.Name = "Renamed";
        repository.Update(camera);

        Assert.Equal("Renamed", Assert.Single(repository.GetAll()).Name);
    }

    [Fact]
    public void Delete_RemovesCamera()
    {
        var repository = CreateRepository();
        var camera = new CameraEntity { Name = "ToDelete" };
        repository.Add(camera);

        repository.Delete(camera.Id);

        Assert.Empty(repository.GetAll());
    }

    [Fact]
    public void Add_WhenDatabaseUnavailable_LogsErrorAndRethrows()
    {
        // A file already occupies the path Add needs as a directory, so the underlying
        // CreateConnection/Directory.CreateDirectory throws before any SQL runs.
        File.WriteAllText(_tempDirectory, "not a directory");
        var recorder = new RecordingLogger();
        AppLog.Initialize(recorder);
        var repository = new SQLiteCameraRepository(new DatabaseService(_tempDirectory));
        var camera = new CameraEntity { Name = "Unreachable" };

        var thrown = Record.Exception(() => repository.Add(camera));
        Assert.NotNull(thrown);

        var call = Assert.Single(recorder.Calls);
        Assert.Equal(LogLevel.Error, call.Level);
        Assert.NotNull(call.Exception);
    }

    [Fact]
    public void GetAll_WhenDatabaseUnavailable_LogsErrorAndRethrows()
    {
        File.WriteAllText(_tempDirectory, "not a directory");
        var recorder = new RecordingLogger();
        AppLog.Initialize(recorder);
        var repository = new SQLiteCameraRepository(new DatabaseService(_tempDirectory));

        var thrown = Record.Exception(() => repository.GetAll());
        Assert.NotNull(thrown);

        var call = Assert.Single(recorder.Calls);
        Assert.Equal(LogLevel.Error, call.Level);
        Assert.NotNull(call.Exception);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
            else if (File.Exists(_tempDirectory))
            {
                File.Delete(_tempDirectory);
            }
        }
        catch
        {
            // best-effort cleanup
        }
    }
}
