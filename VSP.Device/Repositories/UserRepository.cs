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

    public User? GetByNormalizedUsername(string normalizedUsername)
    {
        return _userRepository.GetByNormalizedUsername(normalizedUsername);
    }

    public User? GetById(Guid id)
    {
        return _userRepository.GetById(id);
    }

    public List<User> GetAll()
    {
        return _userRepository.GetAll();
    }

    public void Add(User user)
    {
        _userRepository.Add(user);
    }

    public void Update(User user)
    {
        _userRepository.Update(user);
    }
}
