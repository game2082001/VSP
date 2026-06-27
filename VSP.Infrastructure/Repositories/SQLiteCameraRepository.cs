using Microsoft.Data.Sqlite;
using VSP.Domain.Entities;
using VSP.Domain.Enums;
using VSP.Infrastructure.Database;

namespace VSP.Infrastructure.Repositories;

public class SQLiteCameraRepository
{
    private readonly DatabaseService _databaseService;

    public SQLiteCameraRepository(DatabaseService databaseService)
    {
        _databaseService = databaseService;
    }

    public void Add(Camera camera)
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
    Username, Password, RtspUrl,
    Status, Recording,
    CreateTime, LastModifyTime
)
VALUES
(
    $Id, $Name, $IpAddress, $Brand, $ConnectionType,
    $Model, $Location,
    $HttpPort, $RtspPort, $SdkPort,
    $Username, $Password, $RtspUrl,
    $Status, $Recording,
    $CreateTime, $LastModifyTime
);";

        FillParameters(command, camera);
        command.ExecuteNonQuery();
    }

    public List<Camera> GetAll()
    {
        var cameras = new List<Camera>();

        using var connection = _databaseService.CreateConnection();
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Camera ORDER BY Name;";

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
                Password = reader.IsDBNull(11) ? "" : reader.GetString(11),
                RtspUrl = reader.IsDBNull(12) ? "" : reader.GetString(12),
                Status = reader.IsDBNull(13) ? CameraStatus.Offline : (CameraStatus)reader.GetInt32(13),
                Recording = !reader.IsDBNull(14) && reader.GetInt32(14) == 1,
                CreateTime = reader.IsDBNull(15) ? DateTime.Now : DateTime.Parse(reader.GetString(15)),
                LastModifyTime = reader.IsDBNull(16) ? DateTime.Now : DateTime.Parse(reader.GetString(16))
            });
        }

        return cameras;
    }

    public void Update(Camera camera)
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
    Password = $Password,
    RtspUrl = $RtspUrl,
    Status = $Status,
    Recording = $Recording,
    CreateTime = $CreateTime,
    LastModifyTime = $LastModifyTime
WHERE Id = $Id;
";

        FillParameters(command, camera);
        command.ExecuteNonQuery();
    }

    public void Delete(Guid id)
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
        command.Parameters.AddWithValue("$Password", camera.Password);
        command.Parameters.AddWithValue("$RtspUrl", camera.RtspUrl);
        command.Parameters.AddWithValue("$Status", (int)camera.Status);
        command.Parameters.AddWithValue("$Recording", camera.Recording ? 1 : 0);
        command.Parameters.AddWithValue("$CreateTime", camera.CreateTime.ToString("O"));
        command.Parameters.AddWithValue("$LastModifyTime", camera.LastModifyTime.ToString("O"));
    }
}