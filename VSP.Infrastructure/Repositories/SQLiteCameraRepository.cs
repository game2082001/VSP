using Microsoft.Data.Sqlite;
using VSP.Core.Logging;
using VSP.Domain.Entities;
using VSP.Domain.Enums;
using VSP.Infrastructure.Database;
using VSP.Infrastructure.Security;

namespace VSP.Infrastructure.Repositories;

public class SQLiteCameraRepository
{
    private readonly DatabaseService _databaseService;
    private readonly ICameraCredentialProtector _credentialProtector;

    public SQLiteCameraRepository(DatabaseService databaseService)
        : this(databaseService, new DpapiCurrentUserCameraCredentialProtector())
    {
    }

    public SQLiteCameraRepository(DatabaseService databaseService, ICameraCredentialProtector credentialProtector)
    {
        _databaseService = databaseService;
        _credentialProtector = credentialProtector;
    }

    public void Add(Camera camera)
    {
        Add(camera, CameraCredentialMutation.Clear());
    }

    public void Add(Camera camera, CameraCredentialMutation credentialMutation)
    {
        try
        {
            using var connection = _databaseService.CreateConnection();
            connection.Open();

            using var command = connection.CreateCommand();

            command.CommandText = @"
INSERT INTO Camera
(
    Id, Name, IpAddress, Brand, ConnectionType,
    Model, Location,
    HttpPort, RtspPort, SdkPort,
    Username, PasswordProtected, PasswordProtectionVersion, RtspUrl,
    Status, Recording,
    CreateTime, LastModifyTime
)
VALUES
(
    $Id, $Name, $IpAddress, $Brand, $ConnectionType,
    $Model, $Location,
    $HttpPort, $RtspPort, $SdkPort,
    $Username, $PasswordProtected, $PasswordProtectionVersion, $RtspUrl,
    $Status, $Recording,
    $CreateTime, $LastModifyTime
);";

            FillParameters(command, camera);
            FillCredentialParameters(command, credentialMutation, existing: null);
            command.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            AppLog.Error($"Failed to add camera {camera.Id} to the database.", ex);
            throw;
        }
    }

    public List<Camera> GetAll()
    {
        try
        {
            var cameras = new List<Camera>();

            using var connection = _databaseService.CreateConnection();
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = @"
SELECT Id, Name, IpAddress, Brand, ConnectionType, Model, Location,
       HttpPort, RtspPort, SdkPort, Username, PasswordProtected, PasswordProtectionVersion,
       RtspUrl, Status, Recording, CreateTime, LastModifyTime
FROM Camera
ORDER BY Name;";

            using var reader = command.ExecuteReader();

            while (reader.Read())
            {
                cameras.Add(new Camera
                {
                    Id = Guid.Parse(reader.GetString(0)),
                    Name = reader.GetString(1),
                    IpAddress = reader.GetString(2),
                    Brand = (CameraBrand)reader.GetInt32(3),
                    ConnectionType = (DeviceConnectionType)reader.GetInt32(4),
                    Model = reader.IsDBNull(5) ? "" : reader.GetString(5),
                    Location = reader.IsDBNull(6) ? "" : reader.GetString(6),
                    HttpPort = reader.IsDBNull(7) ? 80 : reader.GetInt32(7),
                    RtspPort = reader.IsDBNull(8) ? 554 : reader.GetInt32(8),
                    SdkPort = reader.IsDBNull(9) ? 8000 : reader.GetInt32(9),
                    Username = reader.IsDBNull(10) ? "" : reader.GetString(10),
                    Password = "",
                    HasStoredPassword = !reader.IsDBNull(11),
                    RtspUrl = reader.IsDBNull(13) ? "" : reader.GetString(13),
                    Status = reader.IsDBNull(14) ? CameraStatus.Offline : (CameraStatus)reader.GetInt32(14),
                    Recording = !reader.IsDBNull(15) && reader.GetInt32(15) == 1,
                    CreateTime = reader.IsDBNull(16) ? DateTime.Now : DateTime.Parse(reader.GetString(16)),
                    LastModifyTime = reader.IsDBNull(17) ? DateTime.Now : DateTime.Parse(reader.GetString(17))
                });
            }

            return cameras;
        }
        catch (Exception ex)
        {
            AppLog.Error("Failed to read cameras from the database.", ex);
            throw;
        }
    }

    public void Update(Camera camera)
    {
        Update(camera, CameraCredentialMutation.Unchanged());
    }

    public void Update(Camera camera, CameraCredentialMutation credentialMutation)
    {
        try
        {
            camera.LastModifyTime = DateTime.Now;

            using var connection = _databaseService.CreateConnection();
            connection.Open();

            using var command = connection.CreateCommand();

            command.CommandText = @"
UPDATE Camera
SET
    Name = $Name,
    IpAddress = $IpAddress,
    Brand = $Brand,
    ConnectionType = $ConnectionType,
    Model = $Model,
    Location = $Location,
    HttpPort = $HttpPort,
    RtspPort = $RtspPort,
    SdkPort = $SdkPort,
    Username = $Username,
    PasswordProtected = $PasswordProtected,
    PasswordProtectionVersion = $PasswordProtectionVersion,
    RtspUrl = $RtspUrl,
    Status = $Status,
    Recording = $Recording,
    CreateTime = $CreateTime,
    LastModifyTime = $LastModifyTime
WHERE Id = $Id;
";

            FillParameters(command, camera);
            FillCredentialParameters(command, credentialMutation, ReadProtectedCredential(connection, camera.Id));
            command.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            AppLog.Error($"Failed to update camera {camera.Id} in the database.", ex);
            throw;
        }
    }

    public CameraCredentials GetCredentials(Guid id)
    {
        try
        {
            using var connection = _databaseService.CreateConnection();
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = @"
SELECT Username, PasswordProtected, PasswordProtectionVersion
FROM Camera
WHERE Id = $Id;";
            command.Parameters.AddWithValue("$Id", id.ToString());

            using var reader = command.ExecuteReader();
            if (!reader.Read())
            {
                return new CameraCredentials("", "");
            }

            var username = reader.IsDBNull(0) ? "" : reader.GetString(0);
            var protectedPassword = ReadProtectedCredential(reader, protectedOrdinal: 1, versionOrdinal: 2);
            return new CameraCredentials(username, Unprotect(protectedPassword));
        }
        catch (Exception ex)
        {
            AppLog.Error($"Failed to read camera credentials for {id}.", ex);
            throw;
        }
    }

    public void Delete(Guid id)
    {
        try
        {
            using var connection = _databaseService.CreateConnection();
            connection.Open();

            using var command = connection.CreateCommand();

            command.CommandText = @"
DELETE FROM Camera
WHERE Id = $Id;
";

            command.Parameters.AddWithValue("$Id", id.ToString());
            command.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            AppLog.Error($"Failed to delete camera {id} from the database.", ex);
            throw;
        }
    }

    private static void FillParameters(SqliteCommand command, Camera camera)
    {
        command.Parameters.AddWithValue("$Id", camera.Id.ToString());
        command.Parameters.AddWithValue("$Name", camera.Name);
        command.Parameters.AddWithValue("$IpAddress", camera.IpAddress);
        command.Parameters.AddWithValue("$Brand", (int)camera.Brand);
        command.Parameters.AddWithValue("$ConnectionType", (int)camera.ConnectionType);
        command.Parameters.AddWithValue("$Model", camera.Model);
        command.Parameters.AddWithValue("$Location", camera.Location);
        command.Parameters.AddWithValue("$HttpPort", camera.HttpPort);
        command.Parameters.AddWithValue("$RtspPort", camera.RtspPort);
        command.Parameters.AddWithValue("$SdkPort", camera.SdkPort);
        command.Parameters.AddWithValue("$Username", camera.Username);
        command.Parameters.AddWithValue("$RtspUrl", camera.RtspUrl);
        command.Parameters.AddWithValue("$Status", (int)camera.Status);
        command.Parameters.AddWithValue("$Recording", camera.Recording ? 1 : 0);
        command.Parameters.AddWithValue("$CreateTime", camera.CreateTime.ToString("O"));
        command.Parameters.AddWithValue("$LastModifyTime", camera.LastModifyTime.ToString("O"));
    }

    private void FillCredentialParameters(
        SqliteCommand command,
        CameraCredentialMutation credentialMutation,
        ProtectedCredential? existing)
    {
        ArgumentNullException.ThrowIfNull(credentialMutation);

        var credential = credentialMutation.Kind switch
        {
            CameraCredentialMutationKind.Unchanged => existing,
            CameraCredentialMutationKind.Clear => null,
            CameraCredentialMutationKind.Replace => Protect(credentialMutation.Password),
            _ => throw new InvalidOperationException($"Unsupported camera credential mutation: {credentialMutation.Kind}.")
        };

        command.Parameters.AddWithValue("$PasswordProtected", (object?)credential?.Envelope ?? DBNull.Value);
        command.Parameters.AddWithValue("$PasswordProtectionVersion", credential?.Version ?? (object)DBNull.Value);
    }

    private ProtectedCredential? ReadProtectedCredential(SqliteConnection connection, Guid id)
    {
        using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT PasswordProtected, PasswordProtectionVersion
FROM Camera
WHERE Id = $Id;";
        command.Parameters.AddWithValue("$Id", id.ToString());

        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadProtectedCredential(reader, protectedOrdinal: 0, versionOrdinal: 1) : null;
    }

    private static ProtectedCredential? ReadProtectedCredential(
        SqliteDataReader reader,
        int protectedOrdinal,
        int versionOrdinal)
    {
        if (reader.IsDBNull(protectedOrdinal))
        {
            if (!reader.IsDBNull(versionOrdinal))
            {
                throw new InvalidDataException("Camera credential protection metadata is inconsistent.");
            }

            return null;
        }

        if (reader.IsDBNull(versionOrdinal) || reader.GetInt32(versionOrdinal) != CameraCredentialMigration.ProtectionVersion)
        {
            throw new InvalidDataException("Camera credential protection version is unsupported.");
        }

        return new ProtectedCredential((byte[])reader.GetValue(protectedOrdinal), reader.GetInt32(versionOrdinal));
    }

    private ProtectedCredential? Protect(string password)
    {
        if (string.IsNullOrEmpty(password))
        {
            return null;
        }

        return new ProtectedCredential(_credentialProtector.Protect(password), CameraCredentialMigration.ProtectionVersion);
    }

    private string Unprotect(ProtectedCredential? credential)
    {
        return credential is null ? "" : _credentialProtector.Unprotect(credential.Envelope);
    }

    private sealed class ProtectedCredential
    {
        public ProtectedCredential(byte[] envelope, int version)
        {
            Envelope = envelope;
            Version = version;
        }

        public byte[] Envelope { get; }

        public int Version { get; }
    }
}
