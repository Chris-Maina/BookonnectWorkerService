namespace BookonnectWorkerService.Services
{
  public interface ILLMService
  {
    /// <summary>
    /// This method generates embeddings for a list of documents.
    /// </summary>
    /// <param name="documents">The list of documents to embed.</param>
    /// <returns>A list of embeddings (float arrays).</returns>
    public Task<float[][]> GenerateDocumentEmbeddingAsync(IEnumerable<string> documents);

    /// <summary>
    /// Creates formatted document strings from IDs and corresponding data for embedding.
    /// </summary>
    /// <param name="ids">The list of identifiers.</param>
    /// <param name="data">The corresponding data for each ID.</param>
    /// <returns>A list of formatted documents ready for embedding generation.</returns>
    // public IEnumerable<string> CreateDocumentsFromData(IEnumerable<string> ids, IEnumerable<string> data);
  }
}