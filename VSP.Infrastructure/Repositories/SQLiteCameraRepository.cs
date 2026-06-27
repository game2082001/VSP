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
    Id,
    Name,
    IpAddress,
    Brand,
    Username,
    Password,
    RtspUrl,
    Status,
    Recording,
    CreateTime
)
VALUES
(
    $Id,
    $Name,
    $IpAddress,
    $Brand,
    $Username,
    $Password,
    $RtspUrl,
    $Status,
    $Recording,
    $CreateTime
);";

        command.Parameters.AddWithValue("$Id", camera.Id.ToString());
        command.Parameters.AddWithValue("$Name", camera.Name);
        command.Parameters.AddWithValue("$IpAddress", camera.IpAddress);
        command.Parameters.AddWithValue("$Brand", (int)camera.Brand);
        command.Parameters.AddWithValue("$Username", camera.Username);
        command.Parameters.AddWithValue("$Password", camera.Password);
        command.Parameters.AddWithValue("$RtspUrl", camera.RtspUrl);
        command.Parameters.AddWithValue("$Status", (int)camera.Status);
        command.Parameters.AddWithValue("$Recording", camera.Recording ? 1 : 0);
        command.Parameters.AddWithValue("$CreateTime", camera.CreateTime.ToString("O"));

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
                Username = reader.IsDBNull(4) ? "" : reader.GetString(4),
                Password = reader.IsDBNull(5) ? "" : reader.GetString(5),
                RtspUrl = reader.IsDBNull(6) ? "" : reader.GetString(6),
                Status = reader.IsDBNull(7) ? CameraStatus.Offline : (CameraStatus)reader.GetInt32(7),
                Recording = !reader.IsDBNull(8) && reader.GetInt32(8) == 1,
                CreateTime = reader.IsDBNull(9) ? DateTime.Now : DateTime.Parse(reader.GetString(9))
            });
        }


        return cameras;
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
}