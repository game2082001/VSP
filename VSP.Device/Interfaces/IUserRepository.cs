using VSP.Domain.Entities;

namespace VSP.Device.Interfaces;

// Intentionally minimal (unlike ICameraRepository's full CRUD shape) -- Milestone 18B only needs
// to look a user up by username (Login) and persist a password change (Forced Password Change).
// GetAll/Add/Delete are out of scope until a future User Management Epic actually needs them.
public interface IUserRepository
{
    User? GetByUsername(string username);

    void Update(User user);
}
