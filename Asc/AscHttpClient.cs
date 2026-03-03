using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AppStoreConnectCli.Asc.Api;
using AppStoreConnectCli.Asc.Auth;
using Serilog;

namespace AppStoreConnectCli.Asc;

public class AscHttpClient
{
    private readonly HttpClient _http;
    private readonly JwtTokenProvider _jwtProvider;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly int[] RetryableStatusCodes = { 429, 500, 502, 503, 504 };
    private static readonly int[] RetryDelaysSeconds = { 1, 2, 4, 8 };

    public AscHttpClient(HttpClient httpClient, JwtTokenProvider jwtProvider)
    {
        _http = httpClient;
        _jwtProvider = jwtProvider;
    }

    public async Task<T> GetAsync<T>(string path, CancellationToken ct = default)
    {
        return await SendWithRetryAsync<T>(HttpMethod.Get, path, null, ct);
    }

    public async Task<TRes> PostAsync<TReq, TRes>(string path, TReq body, CancellationToken ct = default)
    {
        return await SendWithRetryAsync<TRes>(HttpMethod.Post, path, body, ct);
    }

    public async Task<TRes> PatchAsync<TReq, TRes>(string path, TReq body, CancellationToken ct = default)
    {
        return await SendWithRetryAsync<TRes>(HttpMethod.Patch, path, body, ct);
    }

    public async Task DeleteAsync(string path, CancellationToken ct = default)
    {
        await SendWithRetryAsync<object>(HttpMethod.Delete, path, null, ct);
    }

    /// <summary>
    /// Paginates through all pages of a list endpoint, following the "next" link.
    /// </summary>
    public async Task<List<T>> GetAllPagesAsync<T>(string path, CancellationToken ct = default)
    {
        var results = new List<T>();
        string? nextUrl = path;

        while (nextUrl != null)
        {
            var response = await GetAsync<AscListResponse<T>>(nextUrl, ct);
            results.AddRange(response.Data);
            nextUrl = response.Links?.Next;

            // If next is an absolute URL, strip the base so our HttpClient works
            if (nextUrl != null && nextUrl.StartsWith("https://"))
            {
                var uri = new Uri(nextUrl);
                nextUrl = uri.PathAndQuery;
            }
        }

        return results;
    }

    private async Task<T> SendWithRetryAsync<T>(HttpMethod method, string path, object? body, CancellationToken ct)
    {
        for (int attempt = 0; attempt <= RetryDelaysSeconds.Length; attempt++)
        {
            var request = BuildRequest(method, path, body);
            HttpResponseMessage response;

            try
            {
                response = await _http.SendAsync(request, ct);
            }
            catch (HttpRequestException ex)
            {
                if (attempt < RetryDelaysSeconds.Length)
                {
                    Log.Warning("HTTP request failed (attempt {Attempt}): {Message}. Retrying...", attempt + 1, ex.Message);
                    await Task.Delay(TimeSpan.FromSeconds(RetryDelaysSeconds[attempt]), ct);
                    continue;
                }
                throw;
            }

            var statusCode = (int)response.StatusCode;

            if (response.IsSuccessStatusCode)
            {
                // 204 No Content
                if (response.StatusCode == HttpStatusCode.NoContent)
                    return default!;

                var content = await response.Content.ReadAsStringAsync(ct);
                return JsonSerializer.Deserialize<T>(content, JsonOptions)
                       ?? throw new InvalidOperationException($"Empty response body from {method} {path}");
            }

            if (RetryableStatusCodes.Contains(statusCode) && attempt < RetryDelaysSeconds.Length)
            {
                int delaySeconds = RetryDelaysSeconds[attempt];

                // Respect Retry-After header for 429
                if (statusCode == 429 && response.Headers.TryGetValues("Retry-After", out var retryAfterValues))
                {
                    if (int.TryParse(retryAfterValues.FirstOrDefault(), out int retryAfter))
                        delaySeconds = Math.Max(delaySeconds, retryAfter);
                }

                // Add small jitter
                delaySeconds += Random.Shared.Next(0, 500) / 1000;

                Log.Warning("Received {StatusCode} from {Method} {Path} (attempt {Attempt}). Retrying in {Delay}s...",
                    statusCode, method, path, attempt + 1, delaySeconds);
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds), ct);
                continue;
            }

            // Non-retryable errors
            var errorBody = await response.Content.ReadAsStringAsync(ct);

            if (statusCode == 409 || statusCode == 422)
            {
                // Parse Apple's structured error for a clear message
                var detail = ParseAscErrorDetail(errorBody);
                Log.Error("{Status} on {Method} {Path} — {Detail}", statusCode, method, path, detail);
                throw new AscApiException(statusCode, detail);
            }

            throw new AscApiException(statusCode, $"{method} {path} failed with {statusCode}: {errorBody}");
        }

        throw new AscApiException(0, $"Exhausted retries for {method} {path}");
    }

    private static string ParseAscErrorDetail(string errorBody)
    {
        try
        {
            using var doc = JsonDocument.Parse(errorBody);
            if (doc.RootElement.TryGetProperty("errors", out var errors))
            {
                var parts = new List<string>();
                foreach (var err in errors.EnumerateArray())
                {
                    var code = err.TryGetProperty("code", out var c) ? c.GetString() : null;
                    var title = err.TryGetProperty("title", out var t) ? t.GetString() : null;
                    var detail = err.TryGetProperty("detail", out var d) ? d.GetString() : null;
                    parts.Add(string.Join(": ", new[] { code, title, detail }.Where(s => s != null)));
                }
                if (parts.Count > 0)
                    return string.Join(" | ", parts);
            }
        }
        catch (JsonException) { }
        return errorBody;
    }

    private HttpRequestMessage BuildRequest(HttpMethod method, string path, object? body)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _jwtProvider.GetToken());

        if (body != null)
        {
            var json = JsonSerializer.Serialize(body, JsonOptions);
            request.Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        }

        return request;
    }
}

public class AscApiException : Exception
{
    public int StatusCode { get; }

    public AscApiException(int statusCode, string message) : base(message)
    {
        StatusCode = statusCode;
    }
}
