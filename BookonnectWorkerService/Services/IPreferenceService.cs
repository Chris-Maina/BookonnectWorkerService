using BookonnectWorkerService.Models;

namespace BookonnectWorkerService.Services
{
  public interface IPreferenceService
  {
    public Task<IEnumerable<Preference>?> GetAllPreferencesAsync(CancellationToken cancellationToken);
    public Task<Preference?> GetPreferenceByIdAsync(int preferenceId, CancellationToken cancellationToken);
    public Task<IEnumerable<Preference>?> GetPreferencesByUserIdAsync(int userId, CancellationToken cancellationToken);
    public Task AddPreferenceAsync(Preference preference);
    public Task UpdatePreferenceAsync(Preference preference);
    public Task DeletePreferenceAsync(int preferenceId, CancellationToken cancellationToken);
    public Task ProcessUpdatedPreferencesAsync(DateTime since, CancellationToken cancellationToken);
  }
  }