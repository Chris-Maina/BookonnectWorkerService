using BookonnectCore.Services.Interfaces;

namespace BookonnectWorkerService;

public class Worker : BackgroundService
{
  private readonly ILogger<Worker> _logger;
  private readonly IServiceScopeFactory _serviceScopeFactory;
  private readonly TimeSpan _dbSyncInterval = TimeSpan.FromHours(1); // Sync every hour
  private readonly TimeSpan _userUpdateInterval = TimeSpan.FromHours(1); // Update every hour

  public Worker(ILogger<Worker> logger, IServiceScopeFactory serviceScopeFactory)
  {
    _logger = logger;
    _serviceScopeFactory = serviceScopeFactory;
  }

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    // Load initial data once at startup
    try
    {
      // Initialize services
      await InitializeServicesAsync(stoppingToken);

      // Perform business logic
      await PerformDatabaseSyncOperationsAsync(stoppingToken);
      await PerformUserProfileUpdateAsync(stoppingToken);

    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Unexpected error occurred during execution.");
      throw; // Stop the service if initial load fails
    }
  }


  private async Task InitializeServicesAsync(CancellationToken cancellationToken)
  {
    try
    {
      _logger.LogInformation("Starting database initialization...");

      using var scope = _serviceScopeFactory.CreateScope();
      // initialize databases
      var relationalDbService = scope.ServiceProvider.GetRequiredService<IRelationalOperationsService>();
      await relationalDbService.EnsureDatabaseInitializedAsync(cancellationToken);
      _logger.LogInformation("✅ Relational database initialized");

      var vectorDbService = scope.ServiceProvider.GetRequiredService<IVectorOperationsService>();
      await vectorDbService.InitializeAsync(cancellationToken);
      _logger.LogInformation("✅ Vector database initialized");
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "❌ Unexpected error during database initialization");
      throw new InvalidOperationException("Database initialization failed due to unexpected error", ex);
    }
  }

  private async Task PerformDatabaseSyncOperationsAsync(CancellationToken cancellationToken)
  {
    _logger.LogInformation("Starting initial data sync...");
    while (!cancellationToken.IsCancellationRequested)
    {
      try
      {
        using var scope = _serviceScopeFactory.CreateScope();
        var syncService = scope.ServiceProvider.GetRequiredService<IDatabaseSyncService>();

        // check sync status
        var status = await syncService.GetSyncStatusAsync();
        _logger.LogInformation("Sync status RelationalBookCount: {RelationalBookCount}, VectorBookCount: {VectorBookCount}, InSyncCount: {InSyncCount} ({Percentage:F1}%)", status.RelationalBookCount, status.VectorBookCount, status.InSyncCount, status.SyncPercentage);

        if (!status.IsFullySynced)
        {
          // perform sync
          _logger.LogInformation("Databases are not fully synced. Starting sync operation...");
          await syncService.SyncRelationalToVectorDBAsync();

          var newStatus = await syncService.GetSyncStatusAsync();
          _logger.LogInformation("✅ Sync completed. {Percentage:F1}% in sync", newStatus.SyncPercentage);
        }
        else
        {
          _logger.LogInformation("✅ Databases are already in sync. No action needed.");
        }

        // Wait before next sync cycle
        await Task.Delay(_dbSyncInterval, cancellationToken);
      }
      catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
      {
        _logger.LogInformation("Data syncing was cancelled");
        throw;
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "❌ Unexpected error during databases syncing");
        throw new InvalidOperationException("Data sync failed due to unexpected error", ex);
      }
    }
  }

  private async Task PerformUserProfileUpdateAsync(CancellationToken cancellationToken)
  {
    var lastProfileUpdate = DateTime.UtcNow;
    while (!cancellationToken.IsCancellationRequested)
    {
      try
      {
        _logger.LogInformation("Starting user profile update...");

        // Periodically process updated profiles
        if (DateTime.UtcNow - lastProfileUpdate > _userUpdateInterval)
        {
          _logger.LogDebug("Starting periodic user profile update...");
          using var scope = _serviceScopeFactory.CreateScope();
          var preferenceService = scope.ServiceProvider.GetRequiredService<IPreferenceService>();
          await preferenceService.ProcessUpdatedPreferencesAsync(lastProfileUpdate);

          lastProfileUpdate = DateTime.UtcNow;
        }
        // Wait for the user update interval before checking again
        await Task.Delay(_userUpdateInterval, cancellationToken);

        _logger.LogInformation("✅ User profile update completed");
      }
      catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
      {
        _logger.LogInformation("User profile update was cancelled");
        throw;
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "❌ Unexpected error during user profile update");
        throw new InvalidOperationException("User profile update failed due to unexpected error", ex);
      }
    }
  }
}
/**
 * This code defines a Worker class that inherits from BackgroundService.
 * Tasks to be carried out the Worker class:
 * 1. Initialize databases.
 * 2. Periodically sync vector database with relational database.
 * 3. Periodically create/update user profile vectors based on user preferences.
 */
