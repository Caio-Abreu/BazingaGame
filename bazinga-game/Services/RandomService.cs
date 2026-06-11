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
            // External service is down, circuit is open, or request timed out.
            // Fall back to local random so the game keeps working without the dependency.
            logger.LogWarning(ex, "External random service unavailable, falling back to local random.");
            return Random.Shared.Next(1, 6);
        }
    }

    private record RandomResponse([property: System.Text.Json.Serialization.JsonPropertyName("random_number")] int RandomNumber);
}
