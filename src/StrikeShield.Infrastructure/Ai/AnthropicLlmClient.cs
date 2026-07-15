using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StrikeShield.Application.Ai;

namespace StrikeShield.Infrastructure.Ai;

/// <summary>
/// Real BYOK implementation of ILlmClient — calls Anthropic's Messages API
/// directly over HTTP (docs/PHASED_PLAN.md Phase 6). No SDK dependency:
/// one endpoint, one JSON request/response shape, not worth a client
/// library for two narrow, batched call sites (Correlator escalation,
/// Adaptive Planner). Only registered when AiOrchestration:LlmApiKey is
/// configured — NullLlmClient is used otherwise (see
/// Infrastructure.DependencyInjection).
/// </summary>
public class AnthropicLlmClient : ILlmClient
{
    private const string ApiVersion = "2023-06-01";

    private readonly HttpClient _httpClient;
    private readonly AiOrchestrationOptions _options;
    private readonly ILogger<AnthropicLlmClient> _logger;

    public AnthropicLlmClient(HttpClient httpClient, IOptions<AiOrchestrationOptions> options, ILogger<AnthropicLlmClient> logger)
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

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages");
        request.Headers.Add("x-api-key", _options.LlmApiKey);
        request.Headers.Add("anthropic-version", ApiVersion);
        request.Content = JsonContent.Create(new
        {
            model = _options.LlmModel,
            max_tokens = 1024,
            system = systemPrompt + " Respond with a single JSON object and nothing else — no markdown, no code fences, no commentary.",
            messages = new[] { new { role = "user", content = userPrompt } }
        });

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Anthropic API returned {StatusCode} for an AI orchestration call.",
                    response.StatusCode);
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var text = doc.RootElement.GetProperty("content").EnumerateArray().First().GetProperty("text").GetString();

            return text is null ? null : ExtractJsonObject(text);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "AI orchestration LLM call failed.");
            return null;
        }
    }

    /// <summary>
    /// The model is instructed to return raw JSON only, but occasionally
    /// wraps it in a code fence anyway — take the substring between the
    /// first '{' and the matching last '}' rather than trusting the
    /// instruction was followed exactly.
    /// </summary>
    private static string? ExtractJsonObject(string text)
    {
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        return start >= 0 && end > start ? text[start..(end + 1)] : null;
    }
}
