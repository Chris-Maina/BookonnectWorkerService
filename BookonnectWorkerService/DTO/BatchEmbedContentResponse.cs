using System.Text.Json.Serialization;

namespace BookonnectWorkerService.DTO
{
  public class BatchEmbedContentResponse
  {
    [JsonPropertyName("embeddings")]
    public List<ContentEmbedding> Embeddings { get; set; } = new List<ContentEmbedding>();
  }
}