using System.Text.Json.Serialization;

namespace BookonnectWorkerService.DTO
{
  public class EmbedContent
  {
    [JsonPropertyName("parts")]
    public List<EmbedPart> Parts { get; set; } = new List<EmbedPart>();
  }
}