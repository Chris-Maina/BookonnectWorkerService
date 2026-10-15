using System.Text.Json.Serialization;

namespace BookonnectWorkerService.DTO
{
  public class ContentEmbedding
  {
    [JsonPropertyName("values")]
    public float[] Values { get; set; } = Array.Empty<float>();
  }
}