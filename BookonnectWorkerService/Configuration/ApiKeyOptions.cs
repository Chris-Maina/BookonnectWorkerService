namespace BookonnectWorkerService.Configuration;

public class ApiKeyOptions
{
  public const string SectionName = "ApiKey";
  public string Gemini { get; set; } = string.Empty;
}