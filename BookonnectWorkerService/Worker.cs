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

      // Run both periodic jobs concurrently; each loops until cancellation
      await Task.WhenAll(
        PerformDatabaseSyncOperationsAsync(stoppingToken),
        PerformUserProfileUpdateAsync(stoppingToken));
    }
    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
    {
      _logger.LogInformation("Worker is shutting down.");
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Unexpected error occurred during execution.");
      throw; // Stop the service if initialization fails
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
    _logger.LogInformation("Starting database sync loop...");
    while (!cancellationToken.IsCancellationRequested)
    {
      try
      {
        using var scope = _serviceScopeFactory.CreateScope();
        var syncService = scope.ServiceProvider.GetRequiredService<IDatabaseSyncService>();

        var status = await syncService.GetSyncStatusAsync();
        _logger.LogInformation("Sync status RelationalBookCount: {RelationalBookCount}, VectorBookCount: {VectorBookCount}, InSyncCount: {InSyncCount} ({Percentage:F1}%)", status.RelationalBookCount, status.VectorBookCount, status.InSyncCount, status.SyncPercentage);

        if (!status.IsFullySynced)
        {
          _logger.LogInformation("Databases are not fully synced. Starting sync operation...");
          await syncService.SyncRelationalToVectorDBAsync();

          var newStatus = await syncService.GetSyncStatusAsync();
          _logger.LogInformation("✅ Sync completed. {Percentage:F1}% in sync", newStatus.SyncPercentage);
        }
        else
        {
          _logger.LogInformation("✅ Databases are already in sync. No action needed.");
        }
      }
      catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
      {
        break;
      }
      catch (Exception ex)
      {
        // Transient failures should not stop the service; retry next cycle
        _logger.LogError(ex, "❌ Error during database sync. Retrying in {Interval}", _dbSyncInterval);
      }

      if (!await DelayAsync(_dbSyncInterval, cancellationToken)) break;
    }
    _logger.LogInformation("Database sync loop stopped.");
  }

  private async Task PerformUserProfileUpdateAsync(CancellationToken cancellationToken)
  {
    _logger.LogInformation("Starting user profile update loop...");
    // Watermark: only preferences changed after this point are processed
    var lastProfileUpdate = DateTime.UtcNow;
    while (!cancellationToken.IsCancellationRequested)
    {
      if (!await DelayAsync(_userUpdateInterval, cancellationToken)) break;

      try
      {
        var cycleStart = DateTime.UtcNow;
        _logger.LogInformation("Starting user profile update since {Since}...", lastProfileUpdate);

        using var scope = _serviceScopeFactory.CreateScope();
        var preferenceService = scope.ServiceProvider.GetRequiredService<IPreferenceService>();
        await preferenceService.ProcessUpdatedPreferencesAsync(lastProfileUpdate);

        // Advance only on success so failed cycles are retried
        lastProfileUpdate = cycleStart;
        _logger.LogInformation("✅ User profile update completed");
      }
      catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
      {
        break;
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "❌ Error during user profile update. Retrying in {Interval}", _userUpdateInterval);
      }
    }
    _logger.LogInformation("User profile update loop stopped.");
  }

  // Returns false if cancelled during the delay
  private static async Task<bool> DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
  {
    try
    {
      await Task.Delay(delay, cancellationToken);
      return true;
    }
    catch (OperationCanceledException)
    {
      return false;
    }
  }
}
