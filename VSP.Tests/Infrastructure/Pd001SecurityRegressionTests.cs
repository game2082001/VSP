using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using VSP.Core.Logging;
using VSP.Device.Export;
using VSP.Device.Import;
using VSP.Device.Repositories;
using VSP.Domain.Entities;
using VSP.Domain.Enums;
using VSP.Infrastructure.Database;
using VSP.Infrastructure.Repositories;
using VSP.Infrastructure.Security;
using VSP.Tests.Logging;
using VSP.UI.ViewModels;
using Xunit;
using CameraEntity = VSP.Domain.Entities.Camera;

namespace VSP.Tests.Infrastructure;

[Collection("AppLog")]
public sealed class Pd001SecurityRegressionTests : IDisposable
{
    private const string SentinelSecret = "PD001F-SENTINEL-camera-secret-7f0b2d";
    private const string ReplacementSecret = "PD001F-SENTINEL-replacement-secret-5cb36a";
    private const string BatchSecret = "PD001F-SENTINEL-batch-secret-68a4d1";

    private readonly string _tempDirectory =
        Path.Combine(Path.GetTempPath(), $"vsp-pd001f-security-{Guid.NewGuid():N}");

    [Fact]
    public void CameraCredentialPersistence_AddUpdateAndBatchEdit_NeverWritesPlaintextSecretToActiveSqliteFiles()
    {
        var databaseService = CreateInitializedDatabaseService();
        var sqliteRepository = new SQLiteCameraRepository(databaseService);
        var repository = new CameraRepository(sqliteRepository);

        var camera = CreateCamera("PD001F Add", "192.0.2.10");
        repository.Add(camera, CameraCredentialMutation.Replace(SentinelSecret));
        Assert.Equal(SentinelSecret, repository.GetCredentials(camera.Id).Password);
        AssertNoSecretInActiveSqliteFiles(databaseService.GetDatabaseFilePath(), SentinelSecret);

        camera.Location = "Updated";
        repository.Update(camera, CameraCredentialMutation.Replace(ReplacementSecret));
        Assert.Equal(ReplacementSecret, repository.GetCredentials(camera.Id).Password);
        AssertNoSecretInActiveSqliteFiles(databaseService.GetDatabaseFilePath(), ReplacementSecret);

        var batchCamera = CreateCamera("PD001F Batch", "192.0.2.11");
        repository.Add(batchCamera, CameraCredentialMutation.Clear());
        var viewModel = new BatchEditViewModel(new[] { batchCamera }, repository)
        {
            ApplyPassword = true,
            Password = BatchSecret
        };

        viewModel.ApplyCommand.Execute(null);

        Assert.True(viewModel.WasApplied);
        Assert.Equal(BatchSecret, repository.GetCredentials(batchCamera.Id).Password);
        AssertNoSecretInActiveSqliteFiles(databaseService.GetDatabaseFilePath(), BatchSecret);
    }

    [Fact]
    public void LegacyMigrationAndRestore_NeverLeavePlaintextSentinelInActiveOrTemporarySqliteFiles()
    {
        var legacyPath = Path.Combine(_tempDirectory, "legacy-source.db");
        var stagingPath = Path.Combine(_tempDirectory, "vsp.credential-migration.db");
        var legacyBackupPath = Path.Combine(_tempDirectory, "vsp.db.pd001d-legacy");
        Directory.CreateDirectory(_tempDirectory);
        WriteLegacyDatabase(legacyPath, SentinelSecret);

        var migration = new CameraCredentialMigration(new DpapiCurrentUserCameraCredentialProtector());
        migration.Stage(legacyPath, stagingPath);
        AssertNoSecretInExistingFiles(SentinelSecret, stagingPath, stagingPath + "-wal", stagingPath + "-journal");

        migration.Activate(legacyPath, stagingPath, legacyBackupPath);
        AssertNoSecretInActiveSqliteFiles(legacyPath, SentinelSecret);
        Assert.False(File.Exists(stagingPath));
        Assert.False(File.Exists(legacyBackupPath));

        var restoreDirectory = Path.Combine(_tempDirectory, "restore-live");
        var databaseService = new DatabaseService(restoreDirectory);
        new DatabaseInitializer(databaseService).Initialize();
        var liveRepository = new SQLiteCameraRepository(databaseService);
        liveRepository.Add(CreateCamera("Current", "192.0.2.30"), CameraCredentialMutation.Replace(ReplacementSecret));

        var restoreResult = new DatabaseRestoreService(databaseService).Install(CreateLegacyBackup("restore-legacy-source.db", SentinelSecret));

        Assert.True(restoreResult.Success);
        AssertNoSecretInActiveSqliteFiles(databaseService.GetDatabaseFilePath(), SentinelSecret);
        AssertNoSecretInExistingFiles(
            SentinelSecret,
            Path.Combine(restoreDirectory, "vsp.db.restoring.tmp"),
            Path.Combine(restoreDirectory, "vsp.db.restore-prepared.tmp"));
        foreach (var preRestore in Directory.GetFiles(restoreDirectory, "vsp.pre-restore.*.db"))
        {
            AssertNoSecretInExistingFiles(SentinelSecret, preRestore);
        }
    }

    [Fact]
    public void ImportExportAndRestoreMessages_DoNotExposeSentinelSecretOrCiphertext()
    {
        var camera = CreateCamera("Exported", "192.0.2.40");
        camera.Username = "operator";
        camera.Password = SentinelSecret;

        var csv = CameraExportWriter.Write(new[] { camera });
        Assert.DoesNotContain(SentinelSecret, csv);
        Assert.DoesNotContain("Password", csv);
        Assert.DoesNotContain("PasswordProtected", csv);
        Assert.DoesNotContain("Protection", csv);

        var importCsv = $"""
            Name,Brand,Model,IP Address,HTTP Port,RTSP Port,SDK Port,Username,Password,Connection Type,RTSP URL,Location
            Imported,RTSP,Model,192.0.2.41,80,554,8000,user,{SentinelSecret},RTSP,rtsp://example.invalid/live,Lab
            """;
        var importException = Assert.Throws<InvalidDataException>(() =>
            new CsvImportParser().Parse(new MemoryStream(Encoding.UTF8.GetBytes(importCsv))).ToList());
        Assert.DoesNotContain(SentinelSecret, importException.Message);

        var databaseService = CreateInitializedDatabaseService();
        var recorder = new RecordingLogger();
        AppLog.Initialize(recorder);
        var restoreResult = new DatabaseRestoreService(databaseService).ValidateBackupFile(Path.Combine(_tempDirectory, "missing.db"));

        Assert.False(restoreResult.Success);
        Assert.DoesNotContain(SentinelSecret, restoreResult.FailureMessage ?? string.Empty);
        Assert.All(recorder.Calls, call =>
        {
            Assert.DoesNotContain(SentinelSecret, call.Message);
            Assert.DoesNotContain(SentinelSecret, call.Exception?.ToString() ?? string.Empty);
        });
    }

    [Fact]
    public void FileLogger_DoesNotAddCredentialMaterialUnlessCallerSuppliesIt()
    {
        var logsDirectory = Path.Combine(_tempDirectory, "logs");
        var logger = new FileLogger(logsDirectory, () => new DateTime(2026, 8, 24, 12, 0, 0));

        logger.Log(LogLevel.Warning, "Camera credential validation failed without exposing secret material.");

        var logText = File.ReadAllText(logger.GetCurrentLogFilePath());
        Assert.DoesNotContain(SentinelSecret, logText);
        Assert.DoesNotContain("Authorization:", logText);
        Assert.DoesNotContain("rtsp://user:", logText);
    }

    [Fact]
    public void DpapiProtector_FailClosedCasesDoNotExposeSentinelSecret()
    {
        var protector = new DpapiCurrentUserCameraCredentialProtector();
        var envelope = protector.Protect(SentinelSecret);
        envelope[^1] ^= 0x5A;

        var tamperException = Assert.Throws<CryptographicException>(() => protector.Unprotect(envelope));
        Assert.DoesNotContain(SentinelSecret, tamperException.ToString());

        var unsupportedEnvelope = protector.Protect(SentinelSecret);
        unsupportedEnvelope[4] = 99;
        var unsupportedException = Assert.Throws<CryptographicException>(() => protector.Unprotect(unsupportedEnvelope));
        Assert.DoesNotContain(SentinelSecret, unsupportedException.ToString());
    }

    private DatabaseService CreateInitializedDatabaseService()
    {
        var databaseService = new DatabaseService(Path.Combine(_tempDirectory, $"db-{Guid.NewGuid():N}"));
        var result = new DatabaseInitializer(databaseService).Initialize();
        Assert.True(result.Success);
        return databaseService;
    }

    private string CreateLegacyBackup(string fileName, string password)
    {
        var path = Path.Combine(_tempDirectory, fileName);
        WriteLegacyDatabase(path, password);
        return path;
    }

    private static CameraEntity CreateCamera(string name, string ipAddress)
    {
        return new CameraEntity
        {
            Name = name,
            IpAddress = ipAddress,
            Brand = CameraBrand.RTSP,
            ConnectionType = DeviceConnectionType.RTSP,
            Username = "camera-user",
            RtspUrl = $"rtsp://{ipAddress}/stream"
        };
    }

    private static void WriteLegacyDatabase(string path, string password)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false
        }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = @"
CREATE TABLE Camera
(
    Id TEXT PRIMARY KEY,
    Name TEXT NOT NULL,
    IpAddress TEXT NOT NULL,
    Brand INTEGER NOT NULL,
    ConnectionType INTEGER NOT NULL,
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
);

CREATE TABLE User
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
CREATE UNIQUE INDEX IX_User_Username ON User (Username);

INSERT INTO Camera
(Id, Name, IpAddress, Brand, ConnectionType, Model, Location,
 HttpPort, RtspPort, SdkPort, Username, Password, RtspUrl,
 Status, Recording, CreateTime, LastModifyTime)
VALUES
($Id, 'Legacy', '192.0.2.20', 1, 1, '', '',
 80, 554, 8000, 'legacy-user', $Password, 'rtsp://192.0.2.20/stream',
 0, 0, $CreateTime, $LastModifyTime);";
        var now = DateTime.UtcNow.ToString("O");
        command.Parameters.AddWithValue("$Id", Guid.NewGuid().ToString());
        command.Parameters.AddWithValue("$Password", password);
        command.Parameters.AddWithValue("$CreateTime", now);
        command.Parameters.AddWithValue("$LastModifyTime", now);
        command.ExecuteNonQuery();
        SqliteConnection.ClearAllPools();
    }

    private static void AssertNoSecretInActiveSqliteFiles(string databasePath, string secret)
    {
        SqliteConnection.ClearAllPools();
        AssertNoSecretInExistingFiles(secret, databasePath, databasePath + "-wal", databasePath + "-journal");
    }

    private static void AssertNoSecretInExistingFiles(string secret, params string[] paths)
    {
        var needle = Encoding.UTF8.GetBytes(secret);
        foreach (var path in paths)
        {
            if (!File.Exists(path))
            {
                continue;
            }

            Assert.False(ContainsSequence(File.ReadAllBytes(path), needle), $"Secret sentinel was found in {path}.");
        }
    }

    private static bool ContainsSequence(byte[] haystack, byte[] needle) =>
        haystack.AsSpan().IndexOf(needle) >= 0;

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        AppLog.Initialize(new RecordingLogger());
        try
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }
        catch
        {
            // Best-effort test cleanup.
        }
    }
}
