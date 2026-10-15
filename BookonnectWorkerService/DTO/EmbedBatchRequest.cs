using System.Text.Json.Serialization;

namespace BookonnectWorkerService.DTO
{
  public class EmbedBatchRequest
  {
    [JsonPropertyName("requests")]
    public List<EmbedRequest> Requests { get; set; } = new List<EmbedRequest>();
  }
}