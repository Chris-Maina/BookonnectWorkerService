using BookonnectWorkerService.Models;

namespace BookonnectWorkerService.Services
{
  public interface IUserService
  {
    public Task<User?> GetUserByIdAsync(int userId);
    public Task<IEnumerable<User>?> GetAllUsersAsync();
    public Task AddUserAsync(User user);
    public Task UpdateUserAsync(User user);
    public Task DeleteUserAsync(int userId);

  }
}