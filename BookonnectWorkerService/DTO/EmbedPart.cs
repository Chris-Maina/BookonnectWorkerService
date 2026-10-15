using System.Text.Json.Serialization;

namespace BookonnectWorkerService.DTO
{
  public class EmbedPart
  {
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;
  }
}