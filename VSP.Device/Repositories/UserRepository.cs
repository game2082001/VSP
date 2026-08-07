using VSP.Device.Interfaces;
using VSP.Domain.Entities;
using VSP.Infrastructure.Database;
using VSP.Infrastructure.Repositories;

namespace VSP.Device.Repositories;

public class UserRepository : IUserRepository
{
    private readonly SQLiteUserRepository _userRepository;

    public UserRepository()
        : this(new SQLiteUserRepository(new DatabaseService()))
    {
    }

    public UserRepository(SQLiteUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public User? GetByUsername(string username)
    {
        return _userRepository.GetByUsername(username);
    }

    public void Update(User user)
    {
        _userRepository.Update(user);
    }
}
