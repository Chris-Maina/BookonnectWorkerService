namespace BookonnectWorkerService.Services
{
  public interface IDatabaseSeedService
  {
    /// <summary>
    /// Initializes the database. Creates the database and tables if they do not exist.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param
    Task InitializeAsync(CancellationToken cancellationToken);
    /// <summary>
    /// Loads initial data into the database.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param
    Task LoadInitialDataAsync(CancellationToken cancellationToken);

  }
}