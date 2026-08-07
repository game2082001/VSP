using Microsoft.Data.Sqlite;
using VSP.Core.Security;
using VSP.Domain.Enums;

namespace VSP.Infrastructure.Database;

/// <summary>
/// Seeds exactly one default Admin account ("admin"/"admin", MustChangePassword = 1) the first
/// time the User table is empty. Per Product Owner decision (Epic-018 §8.2/§8.3): no default
/// Operator account is seeded, and the default password is never left permanently valid --
/// MustChangePassword forces a change on first successful login (Milestone 18B).
/// </summary>
public static class DefaultAdminSeeder
{
    private const string DefaultUsername = "admin";
    private const string DefaultPassword = "admin";

    public static void SeedIfEmpty(SqliteConnection connection)
    {
        using var countCommand = connection.CreateCommand();
        countCommand.CommandText = "SELECT COUNT(*) FROM User;";
        var count = (long)countCommand.ExecuteScalar()!;

        if (count > 0)
        {
            return;
        }

        var (hash, salt, iterations) = PasswordHasher.Hash(DefaultPassword);
        var now = DateTime.Now.ToString("O");

        using var insertCommand = connection.CreateCommand();
        insertCommand.CommandText = @"
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

        insertCommand.Parameters.AddWithValue("$Id", Guid.NewGuid().ToString());
        insertCommand.Parameters.AddWithValue("$Username", DefaultUsername);
        insertCommand.Parameters.AddWithValue("$PasswordHash", hash);
        insertCommand.Parameters.AddWithValue("$PasswordSalt", salt);
        insertCommand.Parameters.AddWithValue("$PasswordIterations", iterations);
        insertCommand.Parameters.AddWithValue("$Role", (int)Role.Admin);
        insertCommand.Parameters.AddWithValue("$MustChangePassword", 1);
        insertCommand.Parameters.AddWithValue("$CreateTime", now);
        insertCommand.Parameters.AddWithValue("$LastModifyTime", now);

        insertCommand.ExecuteNonQuery();
    }
}
