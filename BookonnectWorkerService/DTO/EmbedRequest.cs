using System.Text.Json.Serialization;
using BookonnectWorkerService.Services;

namespace BookonnectWorkerService.DTO
{
  public class EmbedRequest
  {
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public EmbedContent Content { get; set; } = new EmbedContent();

    [JsonPropertyName("taskType")]
    public string TaskType { get; } = "SEMANTIC_SIMILARITY";

    [JsonPropertyName("outputDimensionality")]
    public int OutputDimensionality { get; } = LLMService.OutputDimensionality;
  }
}