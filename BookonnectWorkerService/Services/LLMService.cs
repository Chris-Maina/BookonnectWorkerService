namespace BookonnectWorkerService.Services
{
  using Microsoft.Extensions.Options;
  using BookonnectWorkerService.Configuration;
  using System.Net.Http;
  using Microsoft.Extensions.Logging;
  using BookonnectWorkerService.DTO;
  using System.Net.Http.Json;
  using System.Text.Json;
  using System.Linq;
  using System.Reflection.Metadata;

  public class LLMService : ILLMService
  {
    private readonly ILogger<LLMService> _logger;
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    public static readonly int OutputDimensionality = 768; // Default gemini-embedding-001 embedding dimension is 3072. Scaling down to 768 for compatibility with ChromaDB storage.
    private const string Model = "models/gemini-embedding-001";
    private const string GeminiEmbedUrl = $"/v1beta/{Model}";

    public LLMService(HttpClient httpClient, ILogger<LLMService> logger, IOptions<ApiKeyOptions> apiKeyOptions)
    {
      _logger = logger;
      _httpClient = httpClient;
      _httpClient.BaseAddress = new Uri("https://generativelanguage.googleapis.com");
      _apiKey = apiKeyOptions.Value.Gemini;
    }

    public async Task<float[][]> GenerateDocumentEmbeddingAsync(IEnumerable<string> documents)
    {
      try
      {
        var (content, endpoint, isBatchRequest) = CreateRequestContent(documents);

        // Send the request and get the response
        var response = await SendEmbeddingRequestAsync(content, endpoint);

        // process the response
        return await ProcessEmbeddingResponseAsync(response, isBatchRequest);
      }
      catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
      {
        _logger.LogError(ex, "Request to Gemini API timed out");
        throw new TimeoutException("Gemini API request timed out", ex);
      }
      catch (HttpRequestException ex)
      {
        _logger.LogError(ex, "HTTP request error");
        throw new HttpRequestException("Error calling Gemini API", ex);
      }
      catch (JsonException ex)
      {
        _logger.LogError(ex, "JSON serialization/deserialization error");
        throw new JsonException("Error processing JSON", ex);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Unexpected error generating embeddings from Gemini API");
        throw;
      }
    }

    /// <summary>
    /// Creates the request content for single or batch embedding requests.
    /// @param documents The documents to embed.
    /// @returns A tuple containing the StringContent, endpoint URL, and a bool indicating if it's a batch request.
    /// </summary>
    private (StringContent Content, string Endpoint, bool IsBatch) CreateRequestContent(IEnumerable<string> documents)
    {
      string embedContent;
      object requestBody;
      bool isBatchRequest = documents.Count() > 1;

      // Construct the request body (JSON payload)
      if (isBatchRequest)
      {
        embedContent = "batchEmbedContents";
        requestBody = CreateBatchEmbeddingRequest(documents);
      }
      else
      {
        embedContent = "embedContent";
        requestBody = CreateSingleEmbeddingRequest(documents.First());
      }

      // Serialize the request body to JSON
      var jsonPayload = JsonSerializer.Serialize(requestBody);
      var content = new StringContent(jsonPayload, System.Text.Encoding.UTF8, "application/json");
      return (content, $"{GeminiEmbedUrl}:{embedContent}", isBatchRequest);
    }

    /// <summary>
    /// Creates a batch embedding request for multiple documents.
    /// @param documents The documents to embed.
    /// @returns The EmbedBatchRequest object.
    /// </summary>
    private EmbedBatchRequest CreateBatchEmbeddingRequest(IEnumerable<string> documents)
    {
      var requests = documents.Select(doc => new EmbedRequest
      {
        Model = Model,
        Content = new EmbedContent
        {
          Parts = new List<EmbedPart> { new EmbedPart { Text = doc } }
        }
      }).ToList();

      return new EmbedBatchRequest
      {
        Requests = requests
      };
    }

    /// <summary>
    /// Creates a single embedding request for one document.
    /// @param document The document to embed.
    /// @returns The EmbedRequest object.
    /// </summary>
    private EmbedRequest CreateSingleEmbeddingRequest(string document)
    {
      return new EmbedRequest
      {
        Model = Model,
        Content = new EmbedContent
        {
          Parts = new List<EmbedPart> { new EmbedPart { Text = document } }
        }
      };
    }

    /// <summary>
    /// Sends the embedding request to the specified endpoint.
    /// </summary>
    private async Task<HttpResponseMessage> SendEmbeddingRequestAsync(StringContent content, string endpoint)
    {
      // Set up the request including required headers
      var requestMessage = new HttpRequestMessage(HttpMethod.Post, endpoint);
      requestMessage.Headers.Add("x-goog-api-key", _apiKey);
      requestMessage.Content = content;

      var response = await _httpClient.SendAsync(requestMessage);
      response.EnsureSuccessStatusCode();

      return response;
    }

    /// <summary>
    /// Processes the embedding response based on whether it's a batch or single request.
    /// @param response The HTTP response message.
    /// @param isBatchRequest Indicates if the request was a batch request.
    /// @returns A jagged array of float embeddings.
    /// </summary>
    private async Task<float[][]> ProcessEmbeddingResponseAsync(HttpResponseMessage response, bool isBatchRequest)
    {
      if (isBatchRequest)
      {
        return await ProcessBatchEmbeddingResponseAsync(response);
      }
      else
      {
        return await ProcessSingleEmbeddingResponseAsync(response);
      }
    }

    /// <summary>
    /// Processes the batch embedding response.
    /// @param response The HTTP response message.
    /// @returns A jagged array of float embeddings.
    /// </summary>
    private async Task<float[][]> ProcessBatchEmbeddingResponseAsync(HttpResponseMessage response)
    {
      var batchResponse = await response.Content.ReadFromJsonAsync<BatchEmbedContentResponse>();
      if (batchResponse?.Embeddings != null)
      {
        return batchResponse.Embeddings
          .Select(e => e.Values)
          .ToArray();
      }
      return Array.Empty<float[]>();
    }

    /// <summary>
    /// Processes the single embedding response.
    /// @param response The HTTP response message.
    /// @returns A jagged array of float embeddings.
    /// </summary>
    private async Task<float[][]> ProcessSingleEmbeddingResponseAsync(HttpResponseMessage response)
    {
      var singleResponse = await response.Content.ReadFromJsonAsync<EmbedContentResponse>();
      if (singleResponse?.Embedding?.Values != null)
      {
        return new float[][] { singleResponse.Embedding.Values };
      }
      return Array.Empty<float[]>();
    }
  }
}