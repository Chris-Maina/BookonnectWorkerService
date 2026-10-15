using System.Text.Json.Serialization;

namespace BookonnectWorkerService.DTO
{
  public class EmbedContentResponse
  {
    [JsonPropertyName("embedding")]
    public ContentEmbedding Embedding { get; set; } = new ContentEmbedding();
  }
}