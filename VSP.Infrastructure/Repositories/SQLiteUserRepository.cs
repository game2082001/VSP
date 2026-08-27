using Microsoft.Data.Sqlite;
using VSP.Core.Logging;
using VSP.Core.Security;
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
            EnsureNormalizedUsername(user);

            using var connection = _databaseService.CreateConnection();
            connection.Open();

            using var command = connection.CreateCommand();

            command.CommandText = @"
INSERT INTO User
(
    Id, Username, NormalizedUsername, PasswordHash, PasswordSalt, PasswordIterations,
    Role, MustChangePassword, IsEnabled,
    CreateTime, LastModifyTime
)
VALUES
(
    $Id, $Username, $NormalizedUsername, $PasswordHash, $PasswordSalt, $PasswordIterations,
    $Role, $MustChangePassword, $IsEnabled,
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
        return GetByNormalizedUsername(UsernameIdentity.Normalize(username));
    }

    public User? GetByNormalizedUsername(string normalizedUsername)
    {
        try
        {
            using var connection = _databaseService.CreateConnection();
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM User WHERE NormalizedUsername = $NormalizedUsername;";
            command.Parameters.AddWithValue("$NormalizedUsername", normalizedUsername);

            using var reader = command.ExecuteReader();

            return reader.Read() ? ReadUser(reader) : null;
        }
        catch (Exception ex)
        {
            AppLog.Error("Failed to read user by normalized username from the database.", ex);
            throw;
        }
    }

    public User? GetById(Guid id)
    {
        try
        {
            using var connection = _databaseService.CreateConnection();
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM User WHERE Id = $Id;";
            command.Parameters.AddWithValue("$Id", id.ToString());

            using var reader = command.ExecuteReader();

            return reader.Read() ? ReadUser(reader) : null;
        }
        catch (Exception ex)
        {
            AppLog.Error($"Failed to read user {id} from the database.", ex);
            throw;
        }
    }

    public void Update(User user)
    {
        try
        {
            EnsureNormalizedUsername(user);
            user.LastModifyTime = DateTime.Now;

            using var connection = _databaseService.CreateConnection();
            connection.Open();

            using var command = connection.CreateCommand();

            command.CommandText = @"
UPDATE User
SET
    Username = $Username,
    NormalizedUsername = $NormalizedUsername,
    PasswordHash = $PasswordHash,
    PasswordSalt = $PasswordSalt,
    PasswordIterations = $PasswordIterations,
    Role = $Role,
    MustChangePassword = $MustChangePassword,
    IsEnabled = $IsEnabled,
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
            Id = Guid.Parse(reader.GetString(reader.GetOrdinal("Id"))),
            Username = reader.GetString(reader.GetOrdinal("Username")),
            NormalizedUsername = reader.GetString(reader.GetOrdinal("NormalizedUsername")),
            PasswordHash = reader.GetString(reader.GetOrdinal("PasswordHash")),
            PasswordSalt = reader.GetString(reader.GetOrdinal("PasswordSalt")),
            PasswordIterations = reader.GetInt32(reader.GetOrdinal("PasswordIterations")),
            Role = (Role)reader.GetInt32(reader.GetOrdinal("Role")),
            MustChangePassword = ReadBoolean(reader, "MustChangePassword", defaultValue: false),
            IsEnabled = ReadBoolean(reader, "IsEnabled", defaultValue: true),
            CreateTime = ReadDateTime(reader, "CreateTime"),
            LastModifyTime = ReadDateTime(reader, "LastModifyTime")
        };
    }

    private static bool ReadBoolean(SqliteDataReader reader, string columnName, bool defaultValue)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? defaultValue : reader.GetInt32(ordinal) == 1;
    }

    private static DateTime ReadDateTime(SqliteDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? DateTime.Now : DateTime.Parse(reader.GetString(ordinal));
    }

    private static void FillParameters(SqliteCommand command, User user)
    {
        command.Parameters.AddWithValue("$Id", user.Id.ToString());
        command.Parameters.AddWithValue("$Username", user.Username);
        command.Parameters.AddWithValue("$NormalizedUsername", user.NormalizedUsername);
        command.Parameters.AddWithValue("$PasswordHash", user.PasswordHash);
        command.Parameters.AddWithValue("$PasswordSalt", user.PasswordSalt);
        command.Parameters.AddWithValue("$PasswordIterations", user.PasswordIterations);
        command.Parameters.AddWithValue("$Role", (int)user.Role);
        command.Parameters.AddWithValue("$MustChangePassword", user.MustChangePassword ? 1 : 0);
        command.Parameters.AddWithValue("$IsEnabled", user.IsEnabled ? 1 : 0);
        command.Parameters.AddWithValue("$CreateTime", user.CreateTime.ToString("O"));
        command.Parameters.AddWithValue("$LastModifyTime", user.LastModifyTime.ToString("O"));
    }

    private static void EnsureNormalizedUsername(User user)
    {
        user.Username = user.Username.Trim();
        user.NormalizedUsername = UsernameIdentity.Normalize(user.Username);
    }
}
