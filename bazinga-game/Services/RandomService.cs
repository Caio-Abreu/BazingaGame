namespace BazingaGame.Services;

public class RandomService(HttpClient httpClient, ILogger<RandomService> logger) : IRandomService
{
    private const string RandomUrl = "https://codechallenge.boohma.com/random";

    public async Task<int> GetRandomChoiceIdAsync()
    {
        try
        {
            var response = await httpClient.GetFromJsonAsync<RandomResponse>(RandomUrl);

            if (response is null)
                throw new InvalidOperationException("Random service returned no data.");

            // Cast to long before Math.Abs: Math.Abs(int.MinValue) overflows and throws.
            return (int)(Math.Abs((long)response.RandomNumber) % 5) + 1;
        }
        catch (Exception ex)
        {
            // Structured properties let observability tools (Datadog, ELK, CloudWatch) filter
            // and alert on this event independently from generic warnings.
            // - ExternalService: identifies which dependency failed (useful when there are many)
            // - FallbackUsed: queryable boolean — "show me all requests that hit the fallback"
            // - ExceptionType: distinguish timeout vs circuit-open vs DNS failure without parsing the message
            logger.LogWarning(ex,
                "External random service unavailable — using local fallback. " +
                "ExternalService={ExternalService} FallbackUsed={FallbackUsed} ExceptionType={ExceptionType}",
                "codechallenge.boohma.com",
                true,
                ex.GetType().Name);

            return Random.Shared.Next(1, 6);
        }
    }

    private record RandomResponse([property: System.Text.Json.Serialization.JsonPropertyName("random_number")] int RandomNumber);
}
