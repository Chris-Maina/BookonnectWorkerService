using System.Globalization;
using BookonnectWorkerService.Models;
using CsvHelper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
namespace BookonnectWorkerService.Services
{
	public class DatabaseSeedService : IDatabaseSeedService
  {
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<DatabaseSeedService> _logger;
    private readonly RelationalDBOptions _rdbOptions;

    public DatabaseSeedService(IServiceScopeFactory serviceScopeFactory, ILogger<DatabaseSeedService> logger, IOptions<RelationalDBOptions> rdbOptions)
    {
      _serviceScopeFactory = serviceScopeFactory;
      _logger = logger;
      _rdbOptions = rdbOptions.Value;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
      try
      {
        _logger.LogInformation("Starting database initialization...");

        /**
         * using scope to create a DbContext instance because:
         * 1. DbContext is registered as a scoped service in the DI container. Matches typical DBContext lifecycle.
         * 2. Each operation (like InitializeAsync) should have its own scope to ensure
         * 3. No shared state or conflicts between operations.
         * 4. Proper disposal of DbContext after use to free resources.
        */
        using var scope = _serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DatabaseContext>();

        // Ensure the database and tables exist
        await dbContext.Database.MigrateAsync(cancellationToken);

        _logger.LogInformation("Database initialization completed successfully");
      }
      catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
      {
        _logger.LogInformation("Database initialization was cancelled");
        throw;
      }
      catch (PostgresException ex)
      {
        _logger.LogError(ex, "PostgreSQL error during database initialization");
        throw new InvalidOperationException("Database operation failed. Check database permissions and configuration.", ex);
      }
      catch (InvalidOperationException ex) when (ex.Message.Contains("Unable to resolve service"))
      {
        _logger.LogError(ex, "Service resolution error for DatabaseContext");
        throw new InvalidOperationException("DatabaseContext not properly registered in dependency injection container.", ex);
      }
      catch (InvalidOperationException ex) when (ex.Source?.Contains("EntityFramework") == true)
      {
        _logger.LogError(ex, "Entity Framework configuration error during database initialization");
        throw new InvalidOperationException("Database configuration error. Check DbContext configuration and connection string.", ex);
      }
      catch (ObjectDisposedException ex)
      {
        _logger.LogError(ex, "Service provider or scope was disposed during initialization");
        throw new InvalidOperationException("Service scope was disposed prematurely.", ex);
      }
      catch (TimeoutException ex)
      {
        _logger.LogError(ex, "Timeout during database initialization");
        throw new InvalidOperationException("Database initialization timed out. Check network connectivity and server performance.", ex);
      }
      catch (UnauthorizedAccessException ex)
      {
        _logger.LogError(ex, "Access denied during database initialization");
        throw new InvalidOperationException("Insufficient permissions to create or access database. Check database user permissions.", ex);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Unexpected error during database initialization");
        throw new InvalidOperationException("Database initialization failed due to unexpected error.", ex);
      }
    }

		public async Task LoadInitialDataAsync(CancellationToken cancellationToken)
    {
      try
      {
        using var reader = new StringReader(await File.ReadAllTextAsync(_rdbOptions.BooksDataFilePath, cancellationToken));
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

        var books = new List<Book>();

        // Read header
        await csv.ReadAsync();
        csv.ReadHeader();

        // Read only first 3 rows
        int rowCount = 0;
        int maxRows = 3;

        while (await csv.ReadAsync() && rowCount < maxRows)
        {
          rowCount++;
          var book = new Book
          {
            ISBN13 = csv.GetField("isbn13") ?? string.Empty,
            ISBN10 = csv.GetField("isbn10") ?? string.Empty,
            Title = csv.GetField("title") ?? string.Empty,
            Authors = csv.GetField("authors") ?? string.Empty,
            Categories = csv.GetField("categories") ?? string.Empty,
            ThumbnailUrl = csv.GetField("thumbnail") ?? string.Empty,
            Description = csv.GetField("description") ?? string.Empty,
            PublishedYear = DateTime.TryParse(csv.GetField("published_year"), out var publishedYear) ? publishedYear : DateTime.MinValue,
            AverageRating = float.TryParse(csv.GetField("average_rating"), out var averageRating) ? averageRating : 0.0f,
            PageCount = int.TryParse(csv.GetField("num_pages"), out var pageCount) ? pageCount : 0,
            RatingsCount = int.TryParse(csv.GetField("ratings_count"), out var ratingsCount) ? ratingsCount : 0
          };
          books.Add(book);
        }

        if (books.Count == 0)
        {
          _logger.LogWarning("No books were successfully parsed from the first {maxRows} rows", maxRows);
          return;
        }


        _logger.LogInformation("Starting initial bulk data load of {count} records from CSV...", books.Count);

        using var scope = _serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DatabaseContext>();

        await dbContext.Books.AddRangeAsync(books, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Successfully completed initial bulk data load from CSV.");
      }
      catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
      {
        _logger.LogInformation("Data loading operation was cancelled");
        throw; // Let cancellation propagate
      }
      catch (FileNotFoundException ex)
      {
        _logger.LogError(ex, "Data file not found: {filePath}", _rdbOptions.BooksDataFilePath);
        throw new InvalidOperationException("Required data file not found. Check file path and ensure data file exists.", ex);
      }
      catch (DirectoryNotFoundException ex)
      {
        _logger.LogError(ex, "Directory not found for data file: {filePath}", _rdbOptions.BooksDataFilePath);
        throw new InvalidOperationException("Data directory not found. Check if directory exists.", ex);
      }
      catch (UnauthorizedAccessException ex)
      {
        _logger.LogError(ex, "Access denied reading data file: {filePath}", _rdbOptions.BooksDataFilePath);
        throw new InvalidOperationException("Access denied to data file. Check file permissions.", ex);
      }
      catch (HeaderValidationException ex)
      {
        _logger.LogError(ex, "CSV header validation failed: {filePath}", _rdbOptions.BooksDataFilePath);
        throw new InvalidOperationException("CSV file has invalid or missing headers.", ex);
      }
      catch (CsvHelperException ex)
      {
        _logger.LogError(ex, "CSV parsing error in file: {filePath}", _rdbOptions.BooksDataFilePath);
        throw new InvalidOperationException("Invalid CSV format or structure. Check file format.", ex);
      }
      catch (DbUpdateException ex)
      {
        _logger.LogError(ex, "Database update error while saving books");
        throw new InvalidOperationException("Failed to save books to database. Check for duplicate records or constraint violations.", ex);
      }
      catch (OutOfMemoryException ex)
      {
        _logger.LogError(ex, "Out of memory while processing large CSV file: {filePath}", _rdbOptions.BooksDataFilePath);
        throw new InvalidOperationException("File too large to process. Consider processing in smaller batches.", ex);
      }
      catch (IOException ex)
      {
        _logger.LogError(ex, "IO error reading data file: {filePath}", _rdbOptions.BooksDataFilePath);
        throw new InvalidOperationException("Error reading data file. File may be locked or corrupted.", ex);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Unexpected error loading initial data from file: {filePath}", _rdbOptions.BooksDataFilePath);
        throw;
      }
    }
	}
}

