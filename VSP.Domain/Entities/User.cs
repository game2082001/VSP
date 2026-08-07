using VSP.Domain.Enums;

namespace VSP.Domain.Entities;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Username { get; set; } = "";

    public string PasswordHash { get; set; } = "";

    public string PasswordSalt { get; set; } = "";

    public int PasswordIterations { get; set; }

    // Defaults to the least-privileged role -- every real construction site (DefaultAdminSeeder,
    // SQLiteUserRepository's row mapping) sets this explicitly, so this only matters if a User is
    // ever constructed without it, and least-privilege is the safe direction to fail in.
    public Role Role { get; set; } = Role.Operator;

    public bool MustChangePassword { get; set; }

    public DateTime CreateTime { get; set; } = DateTime.Now;

    public DateTime LastModifyTime { get; set; } = DateTime.Now;
}
