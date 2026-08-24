using Microsoft.Data.Sqlite;
using VSP.Core.Logging;
using VSP.Infrastructure.Database;
using VSP.Infrastructure.Repositories;
using VSP.Infrastructure.Security;
using VSP.Tests.Logging;
using Xunit;
using CameraEntity = VSP.Domain.Entities.Camera;

namespace VSP.Tests.Infrastructure;

/// <summary>
/// Covers both <see cref="DatabaseRestoreService.ValidateBackupFile"/> (Epic-017 §3.7's six-point
/// checklist) and <see cref="DatabaseRestoreService.Install"/> (§3.4's nine-step flow, including
/// the rollback guarantee). Real temp-directory SQLite files throughout, no mocking, matching
/// the project's existing real-dependency testing convention.
///
/// Two of the flow's nine steps are not independently fault-injected here: step 6 (the final
/// same-volume rename into an already-vacated path) and step 7's re-validation genuinely failing
/// are both extremely difficult to force deterministically through legitimate file-system
/// manipulation once step 5's byte-for-byte copy has already succeeded from an already-validated
/// source -- doing so would require a dedicated fault-injection seam in production code, which
/// this Epic deliberately does not add (no generic backup framework). Step 5's failure -- which
/// happens inside the same vacated window and IS deterministically forceable by locking the
/// fixed temp-install path -- is used below to prove the rollback guarantee end-to-end instead.
/// </summary>
[Collection("AppLog")]
public class DatabaseRestoreServiceTests : IDisposable
{
    private const string OriginalCameraName = "Original";
    private const string ChangedAfterBackupCameraName = "ChangedAfterBackup";
    private const string ProtectedCameraSecret = "restore-preflight-camera-secret";

    private readonly string _tempDirectory =
        Path.Combine(Path.GetTempPath(), $"vsp-db-restore-test-{Guid.NewGuid():N}");

    private string LiveDirectory => Path.Combine(_tempDirectory, "live");

    private DatabaseService CreateLiveDatabaseService() => new(LiveDirectory);

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

    private static byte[] ReadBytes(string path)
    {
        SqliteConnection.ClearAllPools();
        return File.ReadAllBytes(path);
    }

    /// <summary>
    /// Sets up a live database containing <see cref="OriginalCameraName"/>, snapshots it as a
    /// genuine backup (via the real <see cref="DatabaseBackupService"/>) at
    /// <paramref name="backupPath"/>, then changes the live database further so the backup and
    /// the live database provably diverge -- restoring must produce the backup's content, not
    /// the live database's current content.
    /// </summary>
    private DatabaseService SetUpDivergedLiveDatabaseAndBackup(string backupPath)
    {
        var databaseService = CreateLiveDatabaseService();
        new DatabaseInitializer(databaseService).Initialize();
        var repository = new SQLiteCameraRepository(databaseService);
        repository.Add(new CameraEntity
        {
            Name = OriginalCameraName,
            IpAddress = "192.168.1.10",
            Username = "restore-user",
            Password = ProtectedCameraSecret
        });

        var backupResult = new DatabaseBackupService(databaseService).Backup(backupPath);
        Assert.True(backupResult.Success);

        repository.Add(new CameraEntity { Name = ChangedAfterBackupCameraName, IpAddress = "192.168.1.11" });

        return databaseService;
    }

    private static void WriteNonSqliteFile(string path) => File.WriteAllText(path, "not a database");

    private static void WriteSqliteFileWithoutCameraTable(string path)
    {
        using var connection = new SqliteConnection($"Data Source={path}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "CREATE TABLE Foo (Id INTEGER PRIMARY KEY);";
        command.ExecuteNonQuery();
    }

    private static void WriteCorruptedSqliteFile(string path)
    {
        using (var connection = new SqliteConnection($"Data Source={path}"))
        {
            connection.Open();
            using var createCommand = connection.CreateCommand();
            createCommand.CommandText = "CREATE TABLE Camera (Id TEXT PRIMARY KEY, Name TEXT NOT NULL);";
            createCommand.ExecuteNonQuery();

            for (var i = 0; i < 50; i++)
            {
                using var insertCommand = connection.CreateCommand();
                insertCommand.CommandText = "INSERT INTO Camera (Id, Name) VALUES ($id, $name);";
                insertCommand.Parameters.AddWithValue("$id", Guid.NewGuid().ToString());
                insertCommand.Parameters.AddWithValue("$name", new string('x', 200));
                insertCommand.ExecuteNonQuery();
            }
        }

        SqliteConnection.ClearAllPools();

        // Truncate well past the 100-byte file header (so the file is still recognizable as a
        // SQLite database) but short enough that it no longer matches the page count SQLite
        // itself recorded in that header -- a reliable way to produce a file that either fails
        // to open cleanly or opens but fails PRAGMA integrity_check. Either outcome is a
        // validation rejection, which is all this test asserts on.
        var originalLength = new FileInfo(path).Length;
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Write);
        stream.SetLength(originalLength * 2 / 3);
    }

    private static string CreateProtectedBackupFromLegacySource(string sourcePath, string targetPath)
    {
        var stagingPath = targetPath;
        new CameraCredentialMigration(new DpapiCurrentUserCameraCredentialProtector()).Stage(sourcePath, stagingPath);
        return stagingPath;
    }

    private static void TamperFirstProtectedCredential(string databasePath)
    {
        using var connection = new SqliteConnection($"Data Source={databasePath}");
        connection.Open();
        using var select = connection.CreateCommand();
        select.CommandText = @"
SELECT Id, PasswordProtected
FROM Camera
WHERE PasswordProtected IS NOT NULL
ORDER BY Id
LIMIT 1;";
        using var reader = select.ExecuteReader();
        Assert.True(reader.Read());
        var id = reader.GetString(0);
        var envelope = (byte[])reader.GetValue(1);
        envelope[^1] ^= 0x5A;
        reader.Dispose();

        using var update = connection.CreateCommand();
        update.CommandText = "UPDATE Camera SET PasswordProtected = $PasswordProtected WHERE Id = $Id;";
        update.Parameters.AddWithValue("$PasswordProtected", envelope);
        update.Parameters.AddWithValue("$Id", id);
        update.ExecuteNonQuery();
    }

    private static void SetUserVersion(string databasePath, int version)
    {
        using var connection = new SqliteConnection($"Data Source={databasePath}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA user_version = {version};";
        command.ExecuteNonQuery();
    }

    private static void DeleteProtectedCredentialMetadata(string databasePath)
    {
        using var connection = new SqliteConnection($"Data Source={databasePath}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM CredentialMigrationMetadata;";
        command.ExecuteNonQuery();
    }

    // ---------------------------------------------------------------------------------------
    // ValidateBackupFile -- §3.7
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void ValidateBackupFile_GenuineBackup_ReturnsSuccess()
    {
        var backupPath = Path.Combine(_tempDirectory, "genuine-backup.db");
        var databaseService = SetUpDivergedLiveDatabaseAndBackup(backupPath);

        var result = new DatabaseRestoreService(databaseService).ValidateBackupFile(backupPath);

        Assert.True(result.Success);
        Assert.Null(result.FailureMessage);
    }

    [Fact]
    public void PreflightBackupFile_LegacyBackupStagesCredentialConversionWithoutChangingLiveOrSource()
    {
        var backupPath = Path.Combine(_tempDirectory, "genuine-backup.db");
        var databaseService = SetUpDivergedLiveDatabaseAndBackup(backupPath);
        var liveBytesBefore = ReadBytes(databaseService.GetDatabaseFilePath());
        var sourceBytesBefore = ReadBytes(backupPath);

        var result = new DatabaseRestoreService(databaseService).PreflightBackupFile(backupPath);

        Assert.True(result.Success);
        Assert.True(result.CanInstall);
        Assert.False(result.RequiresConfigOnlyRestore);
        Assert.Equal(DatabaseRestorePreflightKind.LegacyPlaintext, result.Kind);
        Assert.Equal(liveBytesBefore, ReadBytes(databaseService.GetDatabaseFilePath()));
        Assert.Equal(sourceBytesBefore, ReadBytes(backupPath));
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(backupPath)!, "vsp.restore-preflight.*.db"));
    }

    [Fact]
    public void PreflightBackupFile_CurrentUserProtectedBackupIsCompatibleButNotInstallableYet()
    {
        var legacyPath = Path.Combine(_tempDirectory, "legacy-source.db");
        var databaseService = SetUpDivergedLiveDatabaseAndBackup(legacyPath);
        var protectedPath = CreateProtectedBackupFromLegacySource(legacyPath, Path.Combine(_tempDirectory, "protected-backup.db"));
        var liveBytesBefore = ReadBytes(databaseService.GetDatabaseFilePath());

        var preflight = new DatabaseRestoreService(databaseService).PreflightBackupFile(protectedPath);
        var validation = new DatabaseRestoreService(databaseService).ValidateBackupFile(protectedPath);

        Assert.True(preflight.Success);
        Assert.False(preflight.CanInstall);
        Assert.True(preflight.RequiresConfigOnlyRestore);
        Assert.Equal(DatabaseRestorePreflightKind.CurrentUserProtectedCredentials, preflight.Kind);
        Assert.False(validation.Success);
        Assert.Contains("not activated yet", validation.FailureMessage);
        Assert.Equal(liveBytesBefore, ReadBytes(databaseService.GetDatabaseFilePath()));
    }

    [Fact]
    public void PreflightBackupFile_ProtectedBackupWithUndecryptableCredentialFailsClosed()
    {
        var legacyPath = Path.Combine(_tempDirectory, "legacy-source.db");
        var databaseService = SetUpDivergedLiveDatabaseAndBackup(legacyPath);
        var protectedPath = CreateProtectedBackupFromLegacySource(legacyPath, Path.Combine(_tempDirectory, "protected-backup.db"));
        TamperFirstProtectedCredential(protectedPath);
        var liveBytesBefore = ReadBytes(databaseService.GetDatabaseFilePath());

        var result = new DatabaseRestoreService(databaseService).PreflightBackupFile(protectedPath);

        Assert.False(result.Success);
        Assert.False(result.CanInstall);
        Assert.Contains("cannot be decrypted", result.FailureMessage);
        Assert.Equal(liveBytesBefore, ReadBytes(databaseService.GetDatabaseFilePath()));
    }

    [Fact]
    public void PreflightBackupFile_ProtectedBackupWithoutMigrationMetadataFailsClosed()
    {
        var legacyPath = Path.Combine(_tempDirectory, "legacy-source.db");
        var databaseService = SetUpDivergedLiveDatabaseAndBackup(legacyPath);
        var protectedPath = CreateProtectedBackupFromLegacySource(legacyPath, Path.Combine(_tempDirectory, "protected-backup.db"));
        DeleteProtectedCredentialMetadata(protectedPath);
        var liveBytesBefore = ReadBytes(databaseService.GetDatabaseFilePath());

        var result = new DatabaseRestoreService(databaseService).PreflightBackupFile(protectedPath);

        Assert.False(result.Success);
        Assert.False(result.CanInstall);
        Assert.Contains("protected VSP database", result.FailureMessage);
        Assert.Equal(liveBytesBefore, ReadBytes(databaseService.GetDatabaseFilePath()));
    }

    [Fact]
    public void PreflightBackupFile_UnknownOrMixedSchemaFailsClosedBeforeInstall()
    {
        var backupPath = Path.Combine(_tempDirectory, "mixed-schema.db");
        var databaseService = SetUpDivergedLiveDatabaseAndBackup(backupPath);
        var liveBytesBefore = ReadBytes(databaseService.GetDatabaseFilePath());
        SetUserVersion(backupPath, 77);

        var result = new DatabaseRestoreService(databaseService).PreflightBackupFile(backupPath);

        Assert.False(result.Success);
        Assert.False(result.CanInstall);
        Assert.Contains("unsupported", result.FailureMessage);
        Assert.Equal(liveBytesBefore, ReadBytes(databaseService.GetDatabaseFilePath()));
    }

    [Fact]
    public void ValidateBackupFile_MissingFile_ReturnsValidationFailed()
    {
        var databaseService = CreateLiveDatabaseService();
        new DatabaseInitializer(databaseService).Initialize();
        var missingPath = Path.Combine(_tempDirectory, "does-not-exist.db");

        var result = new DatabaseRestoreService(databaseService).ValidateBackupFile(missingPath);

        Assert.False(result.Success);
        Assert.Null(result.Exception);
        Assert.NotNull(result.FailureMessage);
    }

    [Fact]
    public void ValidateBackupFile_EmptyFile_ReturnsValidationFailed()
    {
        var databaseService = CreateLiveDatabaseService();
        new DatabaseInitializer(databaseService).Initialize();
        var emptyPath = Path.Combine(_tempDirectory, "empty.db");
        File.WriteAllText(emptyPath, string.Empty);

        var result = new DatabaseRestoreService(databaseService).ValidateBackupFile(emptyPath);

        Assert.False(result.Success);
    }

    [Fact]
    public void ValidateBackupFile_CurrentlyActiveDatabase_ReturnsValidationFailed()
    {
        var databaseService = CreateLiveDatabaseService();
        new DatabaseInitializer(databaseService).Initialize();

        var result = new DatabaseRestoreService(databaseService).ValidateBackupFile(databaseService.GetDatabaseFilePath());

        Assert.False(result.Success);
        Assert.Contains("active database", result.FailureMessage);
    }

    [Fact]
    public void ValidateBackupFile_NonSqliteFile_ReturnsValidationFailedAndLeavesLiveDatabaseUntouched()
    {
        var databaseService = CreateLiveDatabaseService();
        new DatabaseInitializer(databaseService).Initialize();
        var repository = new SQLiteCameraRepository(databaseService);
        repository.Add(new CameraEntity { Name = OriginalCameraName, IpAddress = "192.168.1.10" });

        var notADatabasePath = Path.Combine(_tempDirectory, "not-a-database.db");
        WriteNonSqliteFile(notADatabasePath);

        var result = new DatabaseRestoreService(databaseService).ValidateBackupFile(notADatabasePath);

        Assert.False(result.Success);
        Assert.Equal(new[] { OriginalCameraName }, repository.GetAll().ConvertAll(c => c.Name));
    }

    [Fact]
    public void ValidateBackupFile_SqliteFileWithoutCameraTable_ReturnsValidationFailed()
    {
        var databaseService = CreateLiveDatabaseService();
        new DatabaseInitializer(databaseService).Initialize();
        var noCameraTablePath = Path.Combine(_tempDirectory, "no-camera-table.db");
        WriteSqliteFileWithoutCameraTable(noCameraTablePath);

        var result = new DatabaseRestoreService(databaseService).ValidateBackupFile(noCameraTablePath);

        Assert.False(result.Success);
        Assert.Contains("Camera table", result.FailureMessage);
    }

    [Fact]
    public void ValidateBackupFile_CorruptedFile_ReturnsValidationFailedAndLeavesLiveDatabaseUntouched()
    {
        var databaseService = CreateLiveDatabaseService();
        new DatabaseInitializer(databaseService).Initialize();
        var repository = new SQLiteCameraRepository(databaseService);
        repository.Add(new CameraEntity { Name = OriginalCameraName, IpAddress = "192.168.1.10" });

        var corruptedPath = Path.Combine(_tempDirectory, "corrupted.db");
        WriteCorruptedSqliteFile(corruptedPath);

        var result = new DatabaseRestoreService(databaseService).ValidateBackupFile(corruptedPath);

        Assert.False(result.Success);
        Assert.Equal(new[] { OriginalCameraName }, repository.GetAll().ConvertAll(c => c.Name));
    }

    [Fact]
    public void ValidateBackupFile_Rejection_LogsWarningNotError()
    {
        var databaseService = CreateLiveDatabaseService();
        new DatabaseInitializer(databaseService).Initialize();
        var recorder = new RecordingLogger();
        AppLog.Initialize(recorder);
        var missingPath = Path.Combine(_tempDirectory, "does-not-exist.db");

        new DatabaseRestoreService(databaseService).ValidateBackupFile(missingPath);

        var call = Assert.Single(recorder.Calls);
        Assert.Equal(LogLevel.Warning, call.Level);
    }

    // ---------------------------------------------------------------------------------------
    // Install -- §3.4 steps 4-7 and the step-9 rollback
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void Install_ValidBackup_ReplacesLiveDatabaseAndKeepsPreRestoreCopyAndLeavesSourceUntouched()
    {
        var backupPath = Path.Combine(_tempDirectory, "genuine-backup.db");
        var databaseService = SetUpDivergedLiveDatabaseAndBackup(backupPath);
        var sourceBytesBeforeInstall = File.ReadAllBytes(backupPath);

        var result = new DatabaseRestoreService(databaseService).Install(backupPath);

        Assert.True(result.Success);

        // The backup was taken while only OriginalCameraName existed, before ChangedAfterBackup
        // was added -- so a correct restore must show OriginalCameraName only.
        var repository = new SQLiteCameraRepository(databaseService);
        Assert.Equal(new[] { OriginalCameraName }, repository.GetAll().ConvertAll(c => c.Name));

        var preRestoreFiles = Directory.GetFiles(LiveDirectory, "vsp.pre-restore.*.db");
        var preRestoreFile = Assert.Single(preRestoreFiles);
        Assert.Equal(new[] { OriginalCameraName, ChangedAfterBackupCameraName }, ReadAllCameraNames(preRestoreFile));

        Assert.Equal(sourceBytesBeforeInstall, File.ReadAllBytes(backupPath));
        Assert.False(File.Exists(Path.Combine(LiveDirectory, "vsp.db.restoring.tmp")));
        Assert.Empty(Directory.GetFiles(LiveDirectory, "vsp.restore-preflight.*.db"));
    }

    [Fact]
    public void Install_InvalidSource_ReturnsValidationFailedAndLeavesLiveDatabaseUntouched()
    {
        var databaseService = CreateLiveDatabaseService();
        new DatabaseInitializer(databaseService).Initialize();
        var repository = new SQLiteCameraRepository(databaseService);
        repository.Add(new CameraEntity { Name = OriginalCameraName, IpAddress = "192.168.1.10" });

        var notADatabasePath = Path.Combine(_tempDirectory, "not-a-database.db");
        WriteNonSqliteFile(notADatabasePath);

        var result = new DatabaseRestoreService(databaseService).Install(notADatabasePath);

        Assert.False(result.Success);
        Assert.Null(result.Exception);
        Assert.Equal(new[] { OriginalCameraName }, repository.GetAll().ConvertAll(c => c.Name));
        Assert.Empty(Directory.GetFiles(LiveDirectory, "vsp.pre-restore.*.db"));
    }

    [Fact]
    public void Install_ProtectedBackupIsRejectedBeforeTouchingLiveDatabase()
    {
        var legacyPath = Path.Combine(_tempDirectory, "legacy-source.db");
        var databaseService = SetUpDivergedLiveDatabaseAndBackup(legacyPath);
        var protectedPath = CreateProtectedBackupFromLegacySource(legacyPath, Path.Combine(_tempDirectory, "protected-backup.db"));
        var liveBytesBefore = ReadBytes(databaseService.GetDatabaseFilePath());

        var result = new DatabaseRestoreService(databaseService).Install(protectedPath);

        Assert.False(result.Success);
        Assert.Null(result.Exception);
        Assert.Contains("not activated yet", result.FailureMessage);
        Assert.Equal(liveBytesBefore, ReadBytes(databaseService.GetDatabaseFilePath()));
        Assert.Empty(Directory.GetFiles(LiveDirectory, "vsp.pre-restore.*.db"));
    }

    [Fact]
    public void Install_WhenCurrentDatabaseCannotBeRenamedAside_ReturnsFailedAndLeavesLiveDatabaseUntouched()
    {
        var backupPath = Path.Combine(_tempDirectory, "genuine-backup.db");
        var databaseService = SetUpDivergedLiveDatabaseAndBackup(backupPath);
        var recorder = new RecordingLogger();
        AppLog.Initialize(recorder);

        // Release any pooled native handle left open by the setup calls above -- otherwise the
        // FileStream open below could itself fail for the wrong reason (a sharing conflict with
        // a leftover pooled connection) instead of the intended failure inside Install.
        SqliteConnection.ClearAllPools();

        DatabaseRestoreResult result;
        using (new FileStream(databaseService.GetDatabaseFilePath(), FileMode.Open, FileAccess.Read, FileShare.None))
        {
            // Install's own validation step only ever reads sourceFilePath (the backup), never
            // the live database file, so this exclusive lock on the live vsp.db has no effect
            // until step 4 tries to rename it away -- which then fails, because Windows requires
            // FileShare.Delete on every existing open handle for a rename to succeed.
            result = new DatabaseRestoreService(databaseService).Install(backupPath);
        }

        Assert.False(result.Success);
        Assert.NotNull(result.Exception);
        Assert.Contains(recorder.Calls, c => c.Level == LogLevel.Error);

        // repository.GetAll() orders by Name ascending, so "ChangedAfterBackup" sorts before "Original".
        var repository = new SQLiteCameraRepository(databaseService);
        Assert.Equal(
            new[] { ChangedAfterBackupCameraName, OriginalCameraName },
            repository.GetAll().ConvertAll(c => c.Name));
        Assert.Empty(Directory.GetFiles(LiveDirectory, "vsp.pre-restore.*.db"));
    }

    [Fact]
    public void Install_WhenTempInstallFileCannotBeWritten_RollsBackAndLeavesLiveDatabaseUsable()
    {
        var backupPath = Path.Combine(_tempDirectory, "genuine-backup.db");
        var databaseService = SetUpDivergedLiveDatabaseAndBackup(backupPath);
        var recorder = new RecordingLogger();
        AppLog.Initialize(recorder);

        SqliteConnection.ClearAllPools();

        var tempInstallPath = Path.Combine(LiveDirectory, "vsp.db.restoring.tmp");
        DatabaseRestoreResult result;
        using (new FileStream(tempInstallPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
        {
            // Step 4 (rename the live db aside) succeeds normally; step 5's File.Copy into the
            // fixed temp-install path then fails because this handle exclusively holds it open --
            // proving the rollback restores the original database, not just that the failure was
            // reported.
            result = new DatabaseRestoreService(databaseService).Install(backupPath);
        }

        Assert.False(result.Success);
        Assert.NotNull(result.Exception);
        Assert.Contains(recorder.Calls, c => c.Level == LogLevel.Error);

        Assert.True(File.Exists(databaseService.GetDatabaseFilePath()));
        Assert.Empty(Directory.GetFiles(LiveDirectory, "vsp.pre-restore.*.db"));

        // repository.GetAll() orders by Name ascending, so "ChangedAfterBackup" sorts before "Original".
        var repository = new SQLiteCameraRepository(databaseService);
        Assert.Equal(
            new[] { ChangedAfterBackupCameraName, OriginalCameraName },
            repository.GetAll().ConvertAll(c => c.Name));
    }

    public void Dispose()
    {
        try
        {
            SqliteConnection.ClearAllPools();
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
