namespace BookonnectWorkerService.Services;

using ChromaDB.Client;
using Microsoft.Extensions.Options;
using BookonnectWorkerService.Configuration;
using ChromaDB.Client.Models;
using MathNet.Numerics.LinearAlgebra;

public class ChromaService : IChromaService
{
  private readonly ILogger<ChromaService> _logger;
  private readonly string _booksCollectionName = "bookstest";
  private readonly string _usersCollectionName = "userstest";
  private readonly ChromaClient _chromaClient;
  private readonly HttpClient _httpClient;
  private readonly VectorDBOptions _vectorDBOptions;
  private ChromaCollectionClient? _booksCollectionClient;
  private ChromaCollectionClient? _usersCollectionClient;
  private readonly ILLMService _llmService;
  private readonly ChromaConfigurationOptions _chromaConfigurationOptions;

  public ChromaService(ILogger<ChromaService> logger, IOptions<VectorDBOptions> vectorDBOptions, ILLMService llmService, HttpClient httpClient)
  {
    _logger = logger;
    _llmService = llmService;
    _vectorDBOptions = vectorDBOptions.Value;
    _chromaConfigurationOptions = new ChromaConfigurationOptions(uri: _vectorDBOptions.Uri);
    _httpClient = httpClient;
    _chromaClient = new ChromaClient(_chromaConfigurationOptions, _httpClient);
  }

  public async Task InitializeAsync(CancellationToken cancellationToken)
  {
    try
    {
      _logger.LogInformation("Starting ChromaDB initialization...");
      ChromaCollection booksCollection = await _chromaClient.GetOrCreateCollection(_booksCollectionName);
      _booksCollectionClient = new ChromaCollectionClient(booksCollection, _chromaConfigurationOptions, _httpClient);
      _logger.LogInformation("'{collectionName}' collection client is ready.", _booksCollectionName);

      ChromaCollection usersCollection = await _chromaClient.GetOrCreateCollection(_usersCollectionName);
      _usersCollectionClient = new ChromaCollectionClient(usersCollection, _chromaConfigurationOptions, _httpClient);
      _logger.LogInformation("'{collectionName}' collection client is ready.", _usersCollectionName);
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
      _logger.LogInformation("ChromaDB initialization was cancelled");
      throw;
    }
    catch (ChromaException ex) when (ex.Message.Contains("MethodNotAllowed"))
    {
      _logger.LogError(ex, "ChromaDB API method not allowed. Check ChromaDB server version and API compatibility.");
      throw new InvalidOperationException($"ChromaDB API incompatibility detected. Check server version.", ex);
    }
    catch (ChromaException ex) when (ex.Message.Contains("NotFound"))
    {
      _logger.LogError(ex, "ChromaDB collection not found and could not be created");
      throw new InvalidOperationException($"Failed to create or access ChromaDB collection. Check your ChromaDB URI.", ex);
    }
    catch (ChromaException ex)
    {
      _logger.LogError(ex, "Error creating/retrieving collection");
      throw;
    }
    catch (HttpRequestException ex)
    {
      _logger.LogError(ex, "Network error connecting to ChromaDB server for collection");
      throw new InvalidOperationException($"Cannot connect to ChromaDB server. Check if ChromaDB is running and accessible.", ex);
    }
    catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
    {
      _logger.LogError(ex, "Timeout connecting to ChromaDB server for collection");
      throw new TimeoutException($"ChromaDB server connection timed out for collection", ex);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Unexpected error initializing ChromaDB collection");
      throw;
    }
  }

  public async Task LoadInitialDataAsync(CancellationToken cancellationToken)
  {
    if (_booksCollectionClient == null)
    {
      await InitializeAsync(cancellationToken);
    }

    // Load initial data into the Chroma collection
    try
    {
      _logger.LogInformation("Loading initial data into ChromaDB...");
      var documents = new List<string>();
      var ids = new List<string>();

      string[] lines;
      try
      {
        // todo: replace with actual data source, tagged_descriptions.txt
        lines = await File.ReadAllLinesAsync(_vectorDBOptions.BooksDataFilePath, cancellationToken);
      }
      catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
      {
        _logger.LogInformation("Data reading operation was cancelled");
        throw; // Let cancellation propagate
      }
      catch (FileNotFoundException ex)
      {
        _logger.LogError(ex, "Data file not found: {filePath}", _vectorDBOptions.BooksDataFilePath);
        throw new InvalidOperationException("Required data file not found. Check file path and ensure data file exists.", ex);
      }
      catch (DirectoryNotFoundException ex)
      {
        _logger.LogError(ex, "Directory not found for data file: {filePath}", _vectorDBOptions.BooksDataFilePath);
        throw new InvalidOperationException("Data directory not found. Check if directory exists.", ex);
      }
      catch (UnauthorizedAccessException ex)
      {
        _logger.LogError(ex, "Access denied reading data file: {filePath}", _vectorDBOptions.BooksDataFilePath);
        throw new InvalidOperationException("Access denied to data file. Check file permissions.", ex);
      }
      catch (IOException ex)
      {
        _logger.LogError(ex, "IO error reading data file: {filePath}", _vectorDBOptions.BooksDataFilePath);
        throw new InvalidOperationException("Error reading data file. File may be locked or corrupted.", ex);
      }

      foreach (var line in lines)
      {
        if (string.IsNullOrWhiteSpace(line)) continue;
        var parts = line.Split(' ', 2);
        if (parts.Length >= 2)
        {
          ids.Add(parts[0].Trim());
          documents.Add(parts[1].Trim());
        }
      }

      if (documents.Count == 0)
      {
        _logger.LogWarning("No valid documents found in data file");
        return;
      }

      var embeddings = await _llmService.GenerateDocumentEmbeddingAsync(documents.ToArray());

      var chromaEmbeddings = embeddings
          .Select(arr => new ReadOnlyMemory<float>(arr))
          .ToList();

      // Add to ChromaDB collection. Ignores duplicates based on IDs.
      await _booksCollectionClient!.Add(ids, chromaEmbeddings, null, documents);

      _logger.LogInformation("Successfully loaded {count} documents into Chroma Collection '{collectionName}'.", documents.Count, _booksCollectionName);
    }
    catch (InvalidOperationException)
    {
      // Re-throw wrapped exceptions (file errors, embedding errors)
      throw;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error loading vectors into Chroma Collection: {collectionName}", _booksCollectionName);
      throw;
    }
  }

  public async Task<ChromaCollectionEntry?> GetBookEmbeddingAsync(string isbn13, CancellationToken cancellationToken)
  {
    try
    {
      ArgumentException.ThrowIfNullOrWhiteSpace(isbn13);

      if (_booksCollectionClient == null)
      {
        await InitializeAsync(cancellationToken);
      }

      var result = await _booksCollectionClient!.Get(isbn13);

      if (result == null)
      {
        _logger.LogDebug("No embedding found for book {ISBN13}", isbn13);
      }

      return result;
    }
    catch (ArgumentException)
    {
      throw; // Re-throw argument exceptions as-is
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
      _logger.LogWarning("Get book embedding operation was cancelled for {ISBN13}", isbn13);
      throw;
    }
    catch (ChromaException ex) when (ex.Message.Contains("NotFound"))
    {
      _logger.LogWarning("Book embedding not found in ChromaDB for {ISBN13}", isbn13);
      return null; // Return null instead of throwing for not found
    }
    catch (ChromaException ex)
    {
      _logger.LogError(ex, "ChromaDB error retrieving book embedding for {ISBN13}", isbn13);
      throw new InvalidOperationException($"Failed to retrieve book embedding for {isbn13}", ex);
    }
    catch (HttpRequestException ex)
    {
      _logger.LogError(ex, "Network error retrieving book embedding for {ISBN13}", isbn13);
      throw new InvalidOperationException("ChromaDB connection error", ex);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Unexpected error retrieving book embedding for {ISBN13}", isbn13);
      throw new InvalidOperationException($"Failed to get book embedding for {isbn13}", ex);
    }
  }

  public async Task<List<ChromaCollectionEntry>> GetBookEmbeddingsAsync(List<string> isbn13s, CancellationToken cancellationToken)
  {
    try
    {
      ArgumentNullException.ThrowIfNull(isbn13s);

      if (isbn13s.Count == 0)
      {
        _logger.LogWarning("Empty ISBN13 list provided to GetBookEmbeddingsAsync");
        return new List<ChromaCollectionEntry>();
      }

      // Validate ISBNs
      var invalidIsbns = isbn13s.Where(isbn => string.IsNullOrWhiteSpace(isbn)).ToList();
      if (invalidIsbns.Any())
      {
        throw new ArgumentException($"Invalid ISBN13 values found: null or empty entries", nameof(isbn13s));
      }

      if (_booksCollectionClient == null)
      {
        await InitializeAsync(cancellationToken);
      }

      var results = await _booksCollectionClient!.Get(isbn13s);

      if (results.Count != isbn13s.Count)
      {
        var foundIsbns = results.Select(r => r.Id).ToHashSet();
        var missingIsbns = isbn13s.Where(isbn => !foundIsbns.Contains(isbn)).ToList();
        _logger.LogWarning("Some book embeddings not found. Missing: {MissingISBNs}", string.Join(", ", missingIsbns));
      }

      return results;
    }
    catch (ArgumentException)
    {
      throw; // Re-throw argument exceptions as-is
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
      _logger.LogInformation("Get book embeddings operation was cancelled");
      throw;
    }
    catch (ChromaException ex)
    {
      _logger.LogError(ex, "ChromaDB error retrieving book embeddings for {Count} books", isbn13s?.Count ?? 0);
      throw new InvalidOperationException("Failed to retrieve book embeddings from ChromaDB", ex);
    }
    catch (HttpRequestException ex)
    {
      _logger.LogError(ex, "Network error retrieving book embeddings");
      throw new InvalidOperationException("ChromaDB connection error", ex);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Unexpected error retrieving book embeddings for {Count} books", isbn13s?.Count ?? 0);
      throw new InvalidOperationException("Failed to get book embeddings", ex);
    }
  }

  public async Task<List<ChromaCollectionQueryEntry>> GetSimilarBooksAsync(float[] userProfileVector, int topK, CancellationToken cancellationToken)
  {
    try
    {
      ArgumentNullException.ThrowIfNull(userProfileVector);

      if (userProfileVector.Length == 0)
      {
        throw new ArgumentException("User profile vector cannot be empty", nameof(userProfileVector));
      }

      if (topK <= 0)
      {
        throw new ArgumentException("TopK must be positive", nameof(topK));
      }

      if (userProfileVector.Any(f => !float.IsFinite(f)))
      {
        throw new ArgumentException("User profile vector contains invalid values (NaN or Infinity)", nameof(userProfileVector));
      }

      if (_booksCollectionClient == null)
      {
        await InitializeAsync(cancellationToken);
      }

      // Convert to ReadOnlyMemory<float>
      var userProfileVectorMemory = new ReadOnlyMemory<float>(userProfileVector);

      return await _booksCollectionClient!.Query(userProfileVectorMemory, topK);
    }
    catch (ArgumentException)
    {
      throw; // Re-throw argument exceptions as-is
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
      _logger.LogWarning("Get similar books operation was cancelled");
      throw;
    }
    catch (ChromaException ex)
    {
      _logger.LogError(ex, "ChromaDB error querying similar books (topK: {TopK})", topK);
      throw new InvalidOperationException("Failed to query similar books from ChromaDB", ex);
    }
    catch (HttpRequestException ex)
    {
      _logger.LogError(ex, "Network error querying similar books");
      throw new InvalidOperationException("ChromaDB connection error during similarity search", ex);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Unexpected error querying similar books (topK: {TopK})", topK);
      throw new InvalidOperationException("Failed to find similar books", ex);
    }
  }

  public async Task<float[]> CreateUserProfileVectorAsync(int userId, List<string> likedBookIds, List<string> dislikedBookIds)
  {
    // Implementation for creating user profile vector
    try
    {
      if (userId <= 0)
      {
        throw new ArgumentException("User ID must be positive", nameof(userId));
      }

      ArgumentNullException.ThrowIfNull(likedBookIds);
      ArgumentNullException.ThrowIfNull(dislikedBookIds);

      if (likedBookIds.Count == 0 && dislikedBookIds.Count == 0)
      {
        _logger.LogWarning("No book preferences provided for user {UserId}", userId);
        return Array.Empty<float>();
      }

      _logger.LogDebug("Creating profile vector for user {UserId} with {LikedCount} liked and {DislikedCount} disliked books",
        userId, likedBookIds.Count, dislikedBookIds.Count);

      var likedBookEmbeddings = new List<ChromaCollectionEntry>();
      var dislikedBookEmbeddings = new List<ChromaCollectionEntry>();
      try
      {
        likedBookEmbeddings = await GetBookEmbeddingsAsync(likedBookIds, CancellationToken.None);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to retrieve liked book embeddings for user {UserId}", userId);
        throw new InvalidOperationException("Failed to retrieve liked book embeddings", ex);
      }
      try
      {
        dislikedBookEmbeddings = await GetBookEmbeddingsAsync(dislikedBookIds, CancellationToken.None);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to retrieve disliked book embeddings for user {UserId}", userId);
        throw new InvalidOperationException("Failed to retrieve disliked book embeddings", ex);
      }

      if (likedBookEmbeddings.Count == 0 && dislikedBookEmbeddings.Count == 0)
      {
        _logger.LogWarning("No embeddings found for liked and disliked books.");
        return Array.Empty<float>();
      }

      var invalidEmbeddings = likedBookEmbeddings.Concat(dislikedBookEmbeddings)  // Creates new IEnumerable
        .Where(e => !e.Embeddings.HasValue || e.Embeddings.Value.Length != LLMService.OutputDimensionality)
        .ToList();

      if (invalidEmbeddings.Any())
      {
        _logger.LogError("Invalid embedding dimensions found for user {UserId}. Expected {ExpectedDim}, found embeddings with different dimensions",
          userId, LLMService.OutputDimensionality);
        throw new InvalidOperationException("Inconsistent embedding dimensions detected");
      }

      var userProfileVector = Vector<float>.Build.Dense(LLMService.OutputDimensionality);
      var totalWeight = 0.0f;

      // Process liked books with positive weights
      if (likedBookEmbeddings.Count > 0)
      {
        var weight = CalculateLikedWeight(likedBookEmbeddings.Count);
        foreach (var embedding in likedBookEmbeddings)
        {
          // Convert ReadOnlyMemory<float>? to float[] safely
          var embArray = embedding.Embeddings!.Value.ToArray();

          // Validate embedding values
          if (embArray.Any(f => !float.IsFinite(f)))
          {
            _logger.LogWarning("Invalid embedding values found for liked book {BookId} for user {UserId}",
              embedding.Id, userId);
            continue; // Skip this embedding
          }
          var bookVector = Vector<float>.Build.Dense(embArray);
          userProfileVector += bookVector * weight;
          totalWeight += weight;
        }
      }

      // Process disliked books with negative weights
      if (dislikedBookEmbeddings.Count > 0)
      {
        var weight = CalculateDislikedWeight(dislikedBookEmbeddings.Count);
        foreach (var embedding in dislikedBookEmbeddings)
        { // Convert ReadOnlyMemory<float>? to float[] safely
          var embArray = embedding.Embeddings!.Value.ToArray();

          // Validate embedding values
          if (embArray.Any(f => !float.IsFinite(f)))
          {
            _logger.LogWarning("Invalid embedding values found for disliked book {BookId} for user {UserId}",
              embedding.Id, userId);
            continue; // Skip this embedding
          }

          var bookVector = Vector<float>.Build.Dense(embArray);
          userProfileVector -= bookVector * weight;
          totalWeight += weight * 0.5f; // Count disliked weights as half for normalization
        }
      }

      if (totalWeight == 0)
      {
        _logger.LogWarning("No valid embeddings processed for user {UserId}", userId);
        return Array.Empty<float>();
      }

      // Normalize the user profile vector by total weight
      userProfileVector = userProfileVector / totalWeight;

      // Apply final normalization for cosine similarity
      var magnitude = userProfileVector.L2Norm();
      if (magnitude > 0)
      {
        userProfileVector = userProfileVector / (float)magnitude;
      }
      else
      {
        _logger.LogWarning("User profile vector has zero magnitude for user {UserId}", userId);
        return Array.Empty<float>();
      }

      _logger.LogDebug("Successfully created profile vector for user {UserId}", userId);

      return userProfileVector.ToArray();
    }
    catch (ArgumentException)
    {
      throw; // Re-throw argument exceptions as-is
    }
    catch (InvalidOperationException)
    {
      throw; // Re-throw business logic exceptions as-is
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Unexpected error creating user profile vector for user {UserId}", userId);
      throw new InvalidOperationException($"Failed to create user profile vector for user {userId}", ex);
    }
  }

  public async Task<ChromaCollectionEntry?> GetUserProfileVectorAsync(int userId, CancellationToken cancellationToken)
  {
    try
    {
      if (userId <= 0)
      {
        throw new ArgumentException("User ID must be positive", nameof(userId));
      }
      if (_usersCollectionClient == null)
      {
        await InitializeAsync(cancellationToken);
      }

      var result = await _usersCollectionClient!.Get(userId.ToString());

      if (result == null)
      {
        _logger.LogDebug("No profile vector found for user {UserId}", userId);
      }

      return result;

    }
    catch (ArgumentException)
    {
      throw; // Re-throw argument exceptions as-is
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
      _logger.LogInformation("Get user profile vector operation was cancelled for user {UserId}", userId);
      throw;
    }
    catch (ChromaException ex) when (ex.Message.Contains("NotFound"))
    {
      _logger.LogDebug("User profile vector not found in ChromaDB for user {UserId}", userId);
      return null; // Return null instead of throwing for not found
    }
    catch (ChromaException ex)
    {
      _logger.LogError(ex, "ChromaDB error retrieving user profile vector for user {UserId}", userId);
      throw new InvalidOperationException($"Failed to retrieve user profile vector for user {userId}", ex);
    }
    catch (HttpRequestException ex)
    {
      _logger.LogError(ex, "Network error retrieving user profile vector for user {UserId}", userId);
      throw new InvalidOperationException("ChromaDB connection error", ex);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Unexpected error retrieving user profile vector for user {UserId}", userId);
      throw new InvalidOperationException($"Failed to get user profile vector for user {userId}", ex);
    }
  }

  public async Task UpsertUserProfileVectorAsync(int userId, float[] userProfileVector, CancellationToken cancellationToken)
  {
    try
    {
      if (userId <= 0)
      {
        throw new ArgumentException("User ID must be positive", nameof(userId));
      }

      ArgumentNullException.ThrowIfNull(userProfileVector);

      if (userProfileVector.Length == 0)
      {
        throw new ArgumentException("User profile vector cannot be empty", nameof(userProfileVector));
      }

      if (userProfileVector.Any(f => !float.IsFinite(f)))
      {
        throw new ArgumentException("User profile vector contains invalid values (NaN or Infinity)", nameof(userProfileVector));
      }

      if (_usersCollectionClient == null)
      {
        await InitializeAsync(cancellationToken);
      }
      var userIdString = userId.ToString();
      var defaultMetadata = new Dictionary<string, object>
      {
        { "user_id", userIdString }
      };

      await _usersCollectionClient!.Upsert(
        ids: new List<string> { userIdString },
        embeddings: new List<ReadOnlyMemory<float>> { new ReadOnlyMemory<float>(userProfileVector) },
        metadatas: new List<Dictionary<string, object>> { defaultMetadata },
        documents: null
      );
      _logger.LogDebug("Successfully upserted user profile vector for user {UserId}", userId);
    }
    catch (ArgumentException)
    {
      throw; // Re-throw argument exceptions as-is
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
      _logger.LogInformation("Upsert user profile vector operation was cancelled for user {UserId}", userId);
      throw;
    }
    catch (ChromaException ex)
    {
      _logger.LogError(ex, "ChromaDB error upserting user profile vector for user {UserId}", userId);
      throw new InvalidOperationException($"Failed to upsert user profile vector for user {userId}", ex);
    }
    catch (HttpRequestException ex)
    {
      _logger.LogError(ex, "Network error upserting user profile vector for user {UserId}", userId);
      throw new InvalidOperationException("ChromaDB connection error during upsert", ex);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Unexpected error upserting user profile vector for user {UserId}", userId);
      throw new InvalidOperationException($"Failed to upsert user profile vector for user {userId}", ex);
    }
  }

  public async Task UpsertBookEmbeddingAsync(string isbn13, ReadOnlyMemory<float> bookEmbedding, Dictionary<string, object>? metadata, string? document, CancellationToken cancellationToken)
  {
    try
    {
      ArgumentException.ThrowIfNullOrWhiteSpace(isbn13);

      if (bookEmbedding.Length == 0)
      {
        throw new ArgumentException("Book embedding cannot be empty", nameof(bookEmbedding));
      }

      var embeddingArray = bookEmbedding.ToArray();
      if (embeddingArray.Any(f => !float.IsFinite(f)))
      {
        throw new ArgumentException("Book embedding contains invalid values (NaN or Infinity)", nameof(bookEmbedding));
      }

      if (_booksCollectionClient == null)
      {
        await InitializeAsync(cancellationToken);
      }

      var defaultMetadata = new Dictionary<string, object>
      {
        { "book_id", isbn13 }
      };
      await _booksCollectionClient!.Upsert(
        ids: new List<string> { isbn13 },
        embeddings: new List<ReadOnlyMemory<float>> { bookEmbedding },
        metadatas: new List<Dictionary<string, object>> { metadata ?? defaultMetadata },
        documents: document != null ? new List<string> { document } : null
      );
      _logger.LogDebug("Successfully upserted book embedding for {ISBN13}", isbn13);
    }
    catch (ArgumentException)
    {
      throw; // Re-throw argument exceptions as-is
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
      _logger.LogInformation("Upsert book embedding operation was cancelled for {ISBN13}", isbn13);
      throw;
    }
    catch (ChromaException ex)
    {
      _logger.LogError(ex, "ChromaDB error upserting book embedding for {ISBN13}", isbn13);
      throw new InvalidOperationException($"Failed to upsert book embedding for {isbn13}", ex);
    }
    catch (HttpRequestException ex)
    {
      _logger.LogError(ex, "Network error upserting book embedding for {ISBN13}", isbn13);
      throw new InvalidOperationException("ChromaDB connection error during book embedding upsert", ex);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Unexpected error upserting book embedding for {ISBN13}", isbn13);
      throw new InvalidOperationException($"Failed to upsert book embedding for {isbn13}", ex);
    }
  }

  private float CalculateLikedWeight(int likedCount)
  {
    // Higher weight for users with fewer liked books
    // Weight decreases as the number of liked books increases to prevent overfitting
    return Math.Max(0.5f, 2.0f / (float)Math.Sqrt(likedCount));
  }

  private float CalculateDislikedWeight(int dislikedCount)
  {
    // Smaller weight for disliked books to reduce their negative impact/aggressiveness
    // Weight decreases as the number of disliked books increases
    return Math.Max(0.5f, 0.8f / (float)Math.Sqrt(dislikedCount));
  }
}