using System.Diagnostics;
using BookonnectWorkerService.Models;
using Microsoft.EntityFrameworkCore;

namespace BookonnectWorkerService.Services
{
  public class PreferenceService : IPreferenceService
  {
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<PreferenceService> _logger;
    private readonly IChromaService _chromaService; 

    public PreferenceService(IServiceScopeFactory serviceScopeFactory, ILogger<PreferenceService> logger, IChromaService chromaService)
    {
      _serviceScopeFactory = serviceScopeFactory;
      _logger = logger;
      _chromaService = chromaService;
    }

    public async Task<IEnumerable<Preference>?> GetAllPreferencesAsync(CancellationToken cancellationToken)
    {
      try
      {
        using var scope = _serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
        return await dbContext.Preferences.ToListAsync(cancellationToken);
      }
      catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
      {
        _logger.LogInformation("Get all preferences operation was cancelled");
        throw;
      }
      catch (InvalidOperationException ex) when (ex.Message.Contains("Unable to resolve service"))
      {
        _logger.LogError("Service resolution error for DatabaseContext");
        throw new InvalidOperationException("DatabaseContext not properly registered in dependency injection container.");
      }
      catch (OutOfMemoryException ex)
      {
        _logger.LogError(ex, "Out of memory retrieving all preferences - dataset too large");
        throw new InvalidOperationException("Dataset too large to retrieve all preferences", ex);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Unexpected error retrieving all preferences");
        throw new InvalidOperationException("Failed to retrieve preferences", ex);
      }
    }

    public async Task<Preference?> GetPreferenceByIdAsync(int preferenceId, CancellationToken cancellationToken)
    {
      try
      {
        if (preferenceId <= 0)
        {
          throw new ArgumentException("Preference ID must be positive", nameof(preferenceId));
        }
        using var scope = _serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
        return await dbContext.Preferences.FindAsync(new object[] { preferenceId }, cancellationToken);
      }
      catch (ArgumentException)
      {
        throw; // Rethrow as is
      }
      catch (InvalidOperationException ex) when (ex.Message.Contains("Unable to resolve service"))
      {
        _logger.LogError("Service resolution error for DatabaseContext");
        throw new InvalidOperationException("DatabaseContext not properly registered in dependency injection container.");
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Unexpected error retrieving preference by ID: {PreferenceId}", preferenceId);
        throw new InvalidOperationException("Failed to retrieve preference", ex);
      }
    }

    public async Task<IEnumerable<Preference>?> GetPreferencesByUserIdAsync(int userId, CancellationToken cancellationToken)
    {
      try
      {
        if (userId <= 0)
        {
          throw new ArgumentException("User ID must be positive", nameof(userId));
        }
        using var scope = _serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
        return await dbContext.Preferences.Where(p => p.UserId == userId).ToListAsync(cancellationToken);
      }
      catch (ArgumentException)
      {
        throw; // Rethrow as is
      }
      catch (InvalidOperationException ex) when (ex.Message.Contains("Unable to resolve service"))
      {
        _logger.LogError("Service resolution error for DatabaseContext");
        throw new InvalidOperationException("DatabaseContext not properly registered in dependency injection container.");
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Unexpected error retrieving preferences for User ID: {UserId}", userId);
        throw new InvalidOperationException("Failed to retrieve preferences", ex);
      } 
    }

    public async Task AddPreferenceAsync(Preference preference)
    {
      try
      {
        ArgumentNullException.ThrowIfNull(preference);

        if (preference.UserId <= 0)
        {
          throw new ArgumentException("User ID must be positive", nameof(preference));
        }

        if (string.IsNullOrWhiteSpace(preference.BookId))
        {
          throw new ArgumentException("Book ID cannot be null or empty", nameof(preference));
        }

        using var scope = _serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DatabaseContext>();

        // Check for duplicate preference
        var existingPreference = await dbContext.Preferences
          .FirstOrDefaultAsync(p => p.UserId == preference.UserId && p.BookId == preference.BookId);

        if (existingPreference != null)
        {
          throw new InvalidOperationException($"Preference already exists for user {preference.UserId} and book {preference.BookId}");
        }

        dbContext.Preferences.Add(preference);
        await dbContext.SaveChangesAsync();

        _logger.LogInformation("Added preference for user {UserId} and book {BookId}", preference.UserId, preference.BookId);
      }
      catch (ArgumentException ex)
      {
        _logger.LogError(ex, "Invalid argument error adding preference for user {UserId} and book {BookId}", preference.UserId, preference.BookId);
        throw;
      }
      catch (InvalidOperationException ex) when (ex.Message.Contains("Preference already exists"))
      {
        throw; // Re-throw business logic exceptions as-is
      }
      catch (InvalidOperationException ex) when (ex.Message.Contains("Unable to resolve service"))
      {
        _logger.LogError(ex, "Service resolution error for DatabaseContext");
        throw new InvalidOperationException("Database service not available", ex);
      }
      catch (DbUpdateException ex)
      {
        _logger.LogError(ex, "Database update error adding preference for user {UserId}", preference?.UserId);
        throw new InvalidOperationException("Failed to save preference to database", ex);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Unexpected error adding preference for user {UserId} and book {BookId}", preference.UserId, preference.BookId);
        throw new InvalidOperationException("Failed to add preference", ex);
      }
    }

    public async Task UpdatePreferenceAsync(Preference preference)
    {
      try
      {
        ArgumentNullException.ThrowIfNull(preference);

        if (preference.Id <= 0)
        {
          throw new ArgumentException("Preference ID must be positive", nameof(preference));
        }

        using var scope = _serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DatabaseContext>();

        var existingPreference = await dbContext.Preferences.FindAsync(preference.Id);
        if (existingPreference == null)
        {
          throw new InvalidOperationException($"Preference with ID {preference.Id} not found");
        }

        dbContext.Preferences.Update(preference);
        await dbContext.SaveChangesAsync();

        _logger.LogInformation("Updated preference {PreferenceId} for user {UserId}", preference.Id, preference.UserId);
      }
      catch (ArgumentException)
      {
        throw; // Re-throw argument exceptions as-is
      }
      catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
      {
        throw; // Re-throw not found exceptions as-is
      }
      catch (InvalidOperationException ex) when (ex.Message.Contains("Unable to resolve service"))
      {
        _logger.LogError(ex, "Service resolution error for DatabaseContext");
        throw new InvalidOperationException("Database service not available", ex);
      }
      catch (DbUpdateConcurrencyException ex)
      {
        _logger.LogError(ex, "Concurrency conflict updating preference {PreferenceId}", preference?.Id);
        throw new InvalidOperationException("Preference was modified by another process", ex);
      }
      catch (DbUpdateException ex)
      {
        _logger.LogError(ex, "Database update error updating preference {PreferenceId}", preference?.Id);
        throw new InvalidOperationException("Failed to update preference in database", ex);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Unexpected error updating preference {PreferenceId}", preference.Id);
        throw new InvalidOperationException("Failed to update preference", ex);
      }
    }

    public async Task DeletePreferenceAsync(int preferenceId, CancellationToken cancellationToken)
    {
      try
      {
        ArgumentNullException.ThrowIfNull(preferenceId);
        if (preferenceId <= 0)
        {
          throw new ArgumentException("Preference ID must be positive", nameof(preferenceId));
        }

        using var scope = _serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
        var preference = await GetPreferenceByIdAsync(preferenceId, cancellationToken);
        if (preference == null)
        {
          _logger.LogWarning("Attempted to delete non-existent preference {PreferenceId}", preferenceId);
          return; // Idempotent - don't throw if already deleted
        }
        dbContext.Preferences.Remove(preference);
        await dbContext.SaveChangesAsync();
        _logger.LogInformation("Deleted preference {PreferenceId} for user {UserId}", preferenceId, preference.UserId);
      }
      catch (ArgumentException)
      {
        throw; // Re-throw argument exceptions as-is
      }
      catch (InvalidOperationException ex) when (ex.Message.Contains("Unable to resolve service"))
      {
        _logger.LogError(ex, "Service resolution error for DatabaseContext");
        throw new InvalidOperationException("Database service not available", ex);
      }
      catch (DbUpdateException ex)
      {
        _logger.LogError(ex, "Database update error deleting preference {PreferenceId}", preferenceId);
        throw new InvalidOperationException("Failed to delete preference from database", ex);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Unexpected error deleting preference {PreferenceId}", preferenceId);
        throw new InvalidOperationException("Failed to delete preference", ex);
      }
    }

    public async Task ProcessUpdatedPreferencesAsync(DateTime since, CancellationToken cancellationToken)
    {
      _logger.LogDebug("Starting to process preferences updated since {Since}", since);
      try
      {
        using var scope = _serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DatabaseContext>();

        var userIdsOfUpdatedPreferences = await dbContext.Preferences
          .Where(p => p.UpdatedAt >= since || p.CreatedAt >= since)
          .Select(p => p.UserId)
          .Distinct()
          .ToListAsync(cancellationToken);

        if (userIdsOfUpdatedPreferences.Count == 0)
        {
          _logger.LogInformation("No updated preferences found since {Since}", since);
          return;
        }

        foreach (var userId in userIdsOfUpdatedPreferences)
        {
          if (cancellationToken.IsCancellationRequested)
          {
            _logger.LogInformation("Cancellation requested stopping processing");
            break;
          }

          try
          {
            await ProcessUserPreferenceAsync(userId, cancellationToken);
          }
          catch (Exception ex)
          {
            _logger.LogError(ex, "Failed to process preferences for user {UserId}", userId);
          }
        }
        _logger.LogDebug("Successfully completed processing preferences");
      }
      catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
      {
        _logger.LogWarning("Process updated preferences operation was cancelled");
        throw;
      }
      catch (InvalidOperationException ex) when (ex.Message.Contains("Unable to resolve service"))
      {
        _logger.LogError(ex, "Service resolution error for DatabaseContext");
        throw new InvalidOperationException("Database service not available", ex);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Unexpected error processing updated preferences since {Since}", since);
        throw new InvalidOperationException("Failed to process updated preferences", ex);
      }
    }


    private async Task ProcessUserPreferenceAsync(int userId, CancellationToken cancellationToken)
    {
      try
      {
        var preferences = await GetPreferencesByUserIdAsync(userId, cancellationToken);
        if (preferences == null || !preferences.Any())
        {
          _logger.LogWarning("No preferences found for user ID {UserId}", userId);
          return;
        }

        var likedBooks = preferences.Where(p => p.IsLiked).Select(p => p.BookId).ToList();
        var dislikedBooks = preferences.Where(p => !p.IsLiked).Select(p => p.BookId).ToList();

        if (likedBooks.Count == 0 && dislikedBooks.Count == 0)
        {
          _logger.LogWarning("User {UserId} has no valid book preferences", userId);
          return;
        }

        var userProfileVector = await _chromaService.CreateUserProfileVectorAsync(userId, likedBooks, dislikedBooks);

        if (userProfileVector == null || userProfileVector.Length == 0)
        {
          _logger.LogWarning("Failed to create profile vector for user {UserId}", userId);
          return;
        }
        await _chromaService.UpsertUserProfileVectorAsync(userId, userProfileVector, cancellationToken);
        _logger.LogDebug("Successfully processed user ID {UserId} with {LikedCount} liked and {DislikedCount} disliked books", userId, likedBooks.Count, dislikedBooks.Count);
      }
      catch (ArgumentException ex)
      {
        _logger.LogError(ex, "Invalid argument processing user {UserId} preferences", userId);
        throw new InvalidOperationException($"Invalid data for user {userId}", ex);
      }
      catch (InvalidOperationException ex) when (ex.Message.Contains("Database"))
      {
        _logger.LogError(ex, "Database error processing user {UserId} preferences", userId);
        throw; // Re-throw database errors
      }
      catch (TimeoutException ex)
      {
        _logger.LogError(ex, "Timeout processing user {UserId} preferences", userId);
        throw new InvalidOperationException($"Processing timeout for user {userId}", ex);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Unexpected error processing user {UserId} preferences", userId);
        throw new InvalidOperationException($"Failed to process preferences for user {userId}", ex);
      }
    }
  }
}