using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StrikeShield.Application.Ai;

namespace StrikeShield.Infrastructure.Ai;

/// <summary>
/// Real BYOK implementation of ILlmClient — calls DeepSeek's OpenAI-
/// compatible chat-completions API directly over HTTP. Selected instead of
/// AnthropicLlmClient when AiOrchestration:LlmProvider is "deepseek" (see
/// Infrastructure.DependencyInjection); same ILlmClient contract, just a
/// different wire format (Bearer auth, messages[]-in/choices[]-out) than
/// Anthropic's Messages API.
/// </summary>
public class DeepSeekLlmClient : ILlmClient
{
    private const string Endpoint = "https://api.deepseek.com/chat/completions";

    private readonly HttpClient _httpClient;
    private readonly AiOrchestrationOptions _options;
    private readonly ILogger<DeepSeekLlmClient> _logger;

    public DeepSeekLlmClient(HttpClient httpClient, IOptions<AiOrchestrationOptions> options, ILogger<DeepSeekLlmClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.LlmApiKey);

    public async Task<string?> CompleteJsonAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            return null;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.LlmApiKey);
        request.Content = JsonContent.Create(new
        {
            model = _options.LlmModel,
            max_tokens = 1024,
            messages = new[]
            {
                new
                {
                    role = "system",
                    content = systemPrompt + " Respond with a single JSON object and nothing else — no markdown, no code fences, no commentary."
                },
                new { role = "user", content = userPrompt }
            }
        });

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "DeepSeek API returned {StatusCode} for an AI orchestration call.",
                    response.StatusCode);
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var text = doc.RootElement.GetProperty("choices").EnumerateArray().First()
                .GetProperty("message").GetProperty("content").GetString();

            return text is null ? null : ExtractJsonObject(text);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "AI orchestration LLM call failed.");
            return null;
        }
    }

    /// <summary>
    /// Same defensive extraction as AnthropicLlmClient — the model is
    /// instructed to return raw JSON only but occasionally wraps it in a
    /// code fence anyway.
    /// </summary>
    private static string? ExtractJsonObject(string text)
    {
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        return start >= 0 && end > start ? text[start..(end + 1)] : null;
    }
}
