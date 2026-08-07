using Microsoft.Data.Sqlite;
using VSP.Core.Logging;
using VSP.Domain.Entities;
using VSP.Domain.Enums;
using VSP.Infrastructure.Database;

namespace VSP.Infrastructure.Repositories;

public class SQLiteUserRepository
{
    private readonly DatabaseService _databaseService;

    public SQLiteUserRepository(DatabaseService databaseService)
    {
        _databaseService = databaseService;
    }

    public void Add(User user)
    {
        try
        {
            using var connection = _databaseService.CreateConnection();
            connection.Open();

            using var command = connection.CreateCommand();

            command.CommandText = @"
INSERT INTO User
(
    Id, Username, PasswordHash, PasswordSalt, PasswordIterations,
    Role, MustChangePassword,
    CreateTime, LastModifyTime
)
VALUES
(
    $Id, $Username, $PasswordHash, $PasswordSalt, $PasswordIterations,
    $Role, $MustChangePassword,
    $CreateTime, $LastModifyTime
);";

            FillParameters(command, user);
            command.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            AppLog.Error($"Failed to add user {user.Id} to the database.", ex);
            throw;
        }
    }

    public List<User> GetAll()
    {
        try
        {
            var users = new List<User>();

            using var connection = _databaseService.CreateConnection();
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM User ORDER BY Username;";

            using var reader = command.ExecuteReader();

            while (reader.Read())
            {
                users.Add(ReadUser(reader));
            }

            return users;
        }
        catch (Exception ex)
        {
            AppLog.Error("Failed to read users from the database.", ex);
            throw;
        }
    }

    public User? GetByUsername(string username)
    {
        try
        {
            using var connection = _databaseService.CreateConnection();
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM User WHERE Username = $Username;";
            command.Parameters.AddWithValue("$Username", username);

            using var reader = command.ExecuteReader();

            return reader.Read() ? ReadUser(reader) : null;
        }
        catch (Exception ex)
        {
            AppLog.Error($"Failed to read user '{username}' from the database.", ex);
            throw;
        }
    }

    public void Update(User user)
    {
        try
        {
            user.LastModifyTime = DateTime.Now;

            using var connection = _databaseService.CreateConnection();
            connection.Open();

            using var command = connection.CreateCommand();

            command.CommandText = @"
UPDATE User
SET
    Username = $Username,
    PasswordHash = $PasswordHash,
    PasswordSalt = $PasswordSalt,
    PasswordIterations = $PasswordIterations,
    Role = $Role,
    MustChangePassword = $MustChangePassword,
    CreateTime = $CreateTime,
    LastModifyTime = $LastModifyTime
WHERE Id = $Id;
";

            FillParameters(command, user);
            command.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            AppLog.Error($"Failed to update user {user.Id} in the database.", ex);
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
DELETE FROM User
WHERE Id = $Id;
";

            command.Parameters.AddWithValue("$Id", id.ToString());
            command.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            AppLog.Error($"Failed to delete user {id} from the database.", ex);
            throw;
        }
    }

    private static User ReadUser(SqliteDataReader reader)
    {
        return new User
        {
            Id = Guid.Parse(reader.GetString(0)),
            Username = reader.GetString(1),
            PasswordHash = reader.GetString(2),
            PasswordSalt = reader.GetString(3),
            PasswordIterations = reader.GetInt32(4),
            Role = (Role)reader.GetInt32(5),
            MustChangePassword = !reader.IsDBNull(6) && reader.GetInt32(6) == 1,
            CreateTime = reader.IsDBNull(7) ? DateTime.Now : DateTime.Parse(reader.GetString(7)),
            LastModifyTime = reader.IsDBNull(8) ? DateTime.Now : DateTime.Parse(reader.GetString(8))
        };
    }

    private static void FillParameters(SqliteCommand command, User user)
    {
        command.Parameters.AddWithValue("$Id", user.Id.ToString());
        command.Parameters.AddWithValue("$Username", user.Username);
        command.Parameters.AddWithValue("$PasswordHash", user.PasswordHash);
        command.Parameters.AddWithValue("$PasswordSalt", user.PasswordSalt);
        command.Parameters.AddWithValue("$PasswordIterations", user.PasswordIterations);
        command.Parameters.AddWithValue("$Role", (int)user.Role);
        command.Parameters.AddWithValue("$MustChangePassword", user.MustChangePassword ? 1 : 0);
        command.Parameters.AddWithValue("$CreateTime", user.CreateTime.ToString("O"));
        command.Parameters.AddWithValue("$LastModifyTime", user.LastModifyTime.ToString("O"));
    }
}
