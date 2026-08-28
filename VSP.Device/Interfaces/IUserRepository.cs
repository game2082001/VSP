using VSP.Domain.Entities;

namespace VSP.Device.Interfaces;

// User lifecycle policy belongs in services; this repository remains a persistence boundary.
public interface IUserRepository
{
    List<User> GetAll();

    User? GetById(Guid id);

    User? GetByUsername(string username);

    User? GetByNormalizedUsername(string normalizedUsername);

    void Add(User user);

    void Update(User user);
}
