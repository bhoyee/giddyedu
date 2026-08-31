using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace GiddyEdu.Infrastructure.Integrations;

public sealed record IntegrationResult<T>(bool Succeeded, T? Value, string? ErrorCode, int? StatusCode);

public interface IIntegrationClient
{
    Task<IntegrationResult<TResponse>> SendAsync<TRequest, TResponse>(HttpClient client, HttpMethod method, string path, TRequest request, string? idempotencyKey, CancellationToken cancellationToken = default);
}

public sealed class IntegrationClient(ILogger<IntegrationClient> logger) : IIntegrationClient
{
    public async Task<IntegrationResult<TResponse>> SendAsync<TRequest, TResponse>(HttpClient client, HttpMethod method, string path, TRequest request, string? idempotencyKey, CancellationToken cancellationToken = default)
    {
        var retryable = method == HttpMethod.Get || method == HttpMethod.Head || !string.IsNullOrWhiteSpace(idempotencyKey);
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            using var message = new HttpRequestMessage(method, path) { Content = JsonContent.Create(request) };
            if (!string.IsNullOrWhiteSpace(idempotencyKey)) message.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
            try
            {
                using var response = await client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                var transient = response.StatusCode is System.Net.HttpStatusCode.RequestTimeout or System.Net.HttpStatusCode.TooManyRequests || (int)response.StatusCode >= 500;
                if (transient && retryable && attempt < 3) { await DelayAsync(attempt, cancellationToken); continue; }
                if (!response.IsSuccessStatusCode) return new(false, default, "provider_error", (int)response.StatusCode);
                var value = await response.Content.ReadFromJsonAsync<TResponse>(cancellationToken: cancellationToken);
                return new(true, value, null, (int)response.StatusCode);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                if (retryable && attempt < 3) { await DelayAsync(attempt, cancellationToken); continue; }
                logger.LogWarning("External integration timed out for {Path}", path); return new(false, default, "timeout", null);
            }
            catch (HttpRequestException exception)
            {
                if (retryable && attempt < 3) { await DelayAsync(attempt, cancellationToken); continue; }
                logger.LogWarning(exception, "External integration failed for {Path}", path); return new(false, default, "network_error", null);
            }
        }
        return new(false, default, "retry_exhausted", null);
    }

    private static Task DelayAsync(int attempt, CancellationToken cancellationToken) => Task.Delay(TimeSpan.FromMilliseconds(200 * Math.Pow(2, attempt - 1)), cancellationToken);
}
