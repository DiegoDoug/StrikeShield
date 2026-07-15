using StrikeShield.Application.Ai;

namespace StrikeShield.Api.Tests;

/// <summary>
/// Deterministic ILlmClient test double — returns a canned JSON response
/// instead of a live network call, the same "fixture, not live-scan
/// nondeterminism" approach CorrelatorTests.cs already uses for tool
/// output (docs/PHASED_PLAN.md Phase 6).
/// </summary>
public class FakeLlmClient : ILlmClient
{
    private readonly string? _response;

    public FakeLlmClient(string? response, bool isConfigured = true)
    {
        _response = response;
        IsConfigured = isConfigured;
    }

    public bool IsConfigured { get; }

    public int CallCount { get; private set; }

    public Task<string?> CompleteJsonAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default)
    {
        CallCount++;
        return Task.FromResult(_response);
    }
}
