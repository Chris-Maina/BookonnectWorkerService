using BookonnectWorkerService.Models;
using Microsoft.EntityFrameworkCore;

namespace BookonnectWorkerService.Services
{
  public class UserService : IUserService
  {
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public UserService(IServiceScopeFactory serviceScopeFactory)
    {
      _serviceScopeFactory = serviceScopeFactory;
    }

    public async Task<User?> GetUserByIdAsync(int userId)
    {
      using var scope = _serviceScopeFactory.CreateScope();
      var dbContext = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
      return await dbContext.Users.FindAsync(userId);
    }

    public async Task<IEnumerable<User>?> GetUsersByPreferenceIdAsync(int preferenceId)
    {
      using var scope = _serviceScopeFactory.CreateScope();
      var dbContext = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
      return await dbContext.Users.Where(u => u.Preferences.Any(p => p.Id == preferenceId)).ToListAsync();
    }

    public async Task AddUserAsync(User user)
    {
      using var scope = _serviceScopeFactory.CreateScope();
      var dbContext = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
      dbContext.Users.Add(user);
      await dbContext.SaveChangesAsync();
    }

    public async Task UpdateUserAsync(User user)
    {
      using var scope = _serviceScopeFactory.CreateScope();
      var dbContext = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
      dbContext.Users.Update(user);
      await dbContext.SaveChangesAsync();
    }

    public async Task DeleteUserAsync(int userId)
    {
      using var scope = _serviceScopeFactory.CreateScope();
      var dbContext = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
      var user = await GetUserByIdAsync(userId);
      if (user != null)
      {
        dbContext.Users.Remove(user);
        await dbContext.SaveChangesAsync();
      }
    }

    public async Task<IEnumerable<User>?> GetAllUsersAsync()
    {
      using var scope = _serviceScopeFactory.CreateScope();
      var dbContext = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
      return await dbContext.Users.ToListAsync();
    }
  }
}