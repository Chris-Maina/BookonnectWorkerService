using BookonnectWorkerService.Models;
using ChromaDB.Client.Models;

namespace BookonnectWorkerService.Services;

public interface IChromaService
{
  /// <summary>
  /// Initializes the ChromaDB collection client.
  /// </summary>
  /// <returns>Void.</returns>
  Task InitializeAsync(CancellationToken cancellationToken);

  /// <summary>
  /// Loads initial data into the ChromaDB collection.
  /// </summary>
  /// <returns>Void.</returns>
  Task LoadInitialDataAsync(CancellationToken cancellationToken);

  /// <summary>
  /// Creates user profile vector based on user liked & disliked books.
  /// </summary>
  /// <param name="userId">The ID of the user.</param>
  /// <param name="dislikedBookIds">List of book identifiers the user disliked.</param>
  /// <param name="likedBookIds">List of book identifiers the user liked.</param>
  /// <returns>Void.</returns>
  Task<float[]> CreateUserProfileVectorAsync(int userId, List<string> likedBookIds, List<string> dislikedBookIds);

  /// <summary>
  /// Gets the embedding for a  book.
  /// </summary>
  /// <param name="isbn13">The ISBN13 of the book to load.</param>
  /// <returns>Void.</returns>
  Task<ChromaCollectionEntry?> GetBookEmbeddingAsync(string isbn13, CancellationToken cancellationToken);

  /// <summary>
  /// Retrieves embeddings for a list of book identifiers (ISBN13).
  /// </summary>
  /// <param name="isbn13s">List of book identifiers (ISBN13).</param>
  /// <returns>List of embeddings (float arrays).</returns>
  Task<List<ChromaCollectionEntry>> GetBookEmbeddingsAsync(List<string> isbn13s, CancellationToken cancellationToken);

  // TODO: REMOVE UNUSED METHODS BELOW
  /// <summary>
  /// Retrieves similar books based on the user's profile vector.
  /// </summary>
  /// <param name="userProfileVector">The user's profile vector.</param>
  /// <param name="topK">The number of similar books to retrieve.</param>
  /// <returns>List of similar books.</returns>
  Task<List<ChromaCollectionQueryEntry>> GetSimilarBooksAsync(float[] userProfileVector, int topK, CancellationToken cancellationToken);

  // TODO: REMOVE UNUSED METHODS BELOW
  /// <summary>
  /// Retrieves the user profile vector for a specific user.
  /// </summary>
  /// <param name="userId">The ID of the user.</param>
  /// <returns>The user's profile vector.</returns>
  Task<ChromaCollectionEntry?> GetUserProfileVectorAsync(int userId, CancellationToken cancellationToken);

  /// <summary>
  /// Upserts the user profile vector into the ChromaDB collection.
  /// </summary>
  /// <param name="userId">The ID of the user.</param>
  /// <param name="userProfileVector">The user's profile vector.</param>
  /// <returns>Void.</returns>
  Task UpsertUserProfileVectorAsync(int userId, float[] userProfileVector, CancellationToken cancellationToken);

  /// <summary>
  /// Upserts book embeddings into the ChromaDB collection.
  /// </summary>
  /// <param name="isbn13">ISBN13 of the book to upsert.</param>
  /// <param name="bookEmbedding">The embedding vector for the book.</param>
  /// <param name="metadata">The metadata for the book.</param>
  /// <param name="document">The document content for the book.</param>
  /// <returns>Void.</returns>
  /// 
  Task UpsertBookEmbeddingAsync(string isbn13, ReadOnlyMemory<float> bookEmbedding, Dictionary<string, object>? metadata = null, string? document = null, CancellationToken cancellationToken = default);
}