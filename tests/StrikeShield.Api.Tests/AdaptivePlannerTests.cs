using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using StrikeShield.Application.Playbooks;
using StrikeShield.Domain.Entities;
using StrikeShield.Domain.Enums;
using StrikeShield.Infrastructure.Persistence;
using Xunit;

namespace StrikeShield.Api.Tests;

/// <summary>
/// Phase 6 acceptance test (docs/PHASED_PLAN.md): the Adaptive Planner
/// "proposes playbook amendments as a pending, human-approved diff — never
/// auto-executed." Uses a fake ILlmClient with a known, hand-crafted
/// proposal — the live "run a recon-only playbook, expect a pending
/// amendment" flow needs a real BYOK key, same as Strix (Phase 4); this
/// proves the deterministic parts of the pipeline around that call.
/// </summary>
public class AdaptivePlannerTests
{
    private static StrikeShieldDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<StrikeShieldDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new StrikeShieldDbContext(options);
    }

    private static PlaybookStep RemainingStep(string stepKey) => new()
    {
        StepKey = stepKey,
        ToolName = "nuclei",
        ArgsTemplate = "-u {target} -jsonl -o {output}"
    };

    [Fact]
    public async Task ProposeAmendmentAsync_CreatesAPendingAmendment_WhenLlmProposesOne()
    {
        await using var db = CreateInMemoryDbContext();
        var scanJobId = Guid.NewGuid();
        var stepRunId = Guid.NewGuid();
        var targetStep = RemainingStep("nuclei-targeted");

        db.Assets.Add(new Asset
        {
            TargetId = Guid.NewGuid(),
            DiscoveredByStepRunId = stepRunId,
            Type = AssetType.TechFingerprint,
            Value = "WordPress 6.2"
        });
        await db.SaveChangesAsync();

        var fakeResponse = $$"""
        {"proposeAmendment": true, "targetStepKey": "nuclei-targeted", "proposedArgsTemplate": "-u {target} -jsonl -o {output} -tags wordpress", "rationale": "Fingerprinted WordPress"}
        """;
        var planner = new AdaptivePlanner(db, new FakeLlmClient(fakeResponse), NullLogger<AdaptivePlanner>.Instance);

        await planner.ProposeAmendmentAsync(scanJobId, stepRunId, new[] { targetStep }, CancellationToken.None);

        var amendment = await db.PlaybookAmendments.SingleAsync();
        Assert.Equal(PlaybookAmendmentStatus.Pending, amendment.Status);
        Assert.Equal(targetStep.Id, amendment.TargetPlaybookStepId);
        Assert.Equal(stepRunId, amendment.ProposedByStepRunId);
        Assert.Contains("wordpress", amendment.ProposedArgsTemplate);
    }

    [Fact]
    public async Task ProposeAmendmentAsync_CreatesNoAmendment_WhenLlmProposesNothing()
    {
        await using var db = CreateInMemoryDbContext();
        var scanJobId = Guid.NewGuid();
        var stepRunId = Guid.NewGuid();
        var targetStep = RemainingStep("nuclei-targeted");

        db.Assets.Add(new Asset { TargetId = Guid.NewGuid(), DiscoveredByStepRunId = stepRunId, Type = AssetType.Url, Value = "http://example.com/" });
        await db.SaveChangesAsync();

        var planner = new AdaptivePlanner(
            db,
            new FakeLlmClient("{\"proposeAmendment\": false, \"targetStepKey\": null, \"proposedArgsTemplate\": null, \"rationale\": null}"),
            NullLogger<AdaptivePlanner>.Instance);

        await planner.ProposeAmendmentAsync(scanJobId, stepRunId, new[] { targetStep }, CancellationToken.None);

        Assert.Empty(db.PlaybookAmendments);
    }

    [Fact]
    public async Task ProposeAmendmentAsync_NeverCallsTheLlm_WhenNoByokKeyIsConfigured()
    {
        await using var db = CreateInMemoryDbContext();
        var scanJobId = Guid.NewGuid();
        var stepRunId = Guid.NewGuid();
        var targetStep = RemainingStep("nuclei-targeted");

        db.Assets.Add(new Asset { TargetId = Guid.NewGuid(), DiscoveredByStepRunId = stepRunId, Type = AssetType.Url, Value = "http://example.com/" });
        await db.SaveChangesAsync();

        var llmClient = new FakeLlmClient(response: null, isConfigured: false);
        var planner = new AdaptivePlanner(db, llmClient, NullLogger<AdaptivePlanner>.Instance);

        await planner.ProposeAmendmentAsync(scanJobId, stepRunId, new[] { targetStep }, CancellationToken.None);

        Assert.Equal(0, llmClient.CallCount);
        Assert.Empty(db.PlaybookAmendments);
    }

    [Fact]
    public async Task ProposeAmendmentAsync_NeverCallsTheLlm_WhenTheTriggeringStepDiscoveredNoAssets()
    {
        await using var db = CreateInMemoryDbContext();
        var scanJobId = Guid.NewGuid();
        var stepRunId = Guid.NewGuid();
        var targetStep = RemainingStep("nuclei-targeted");

        var llmClient = new FakeLlmClient("{\"proposeAmendment\": true, \"targetStepKey\": \"nuclei-targeted\", \"proposedArgsTemplate\": \"-u {target}\", \"rationale\": \"x\"}");
        var planner = new AdaptivePlanner(db, llmClient, NullLogger<AdaptivePlanner>.Instance);

        await planner.ProposeAmendmentAsync(scanJobId, stepRunId, new[] { targetStep }, CancellationToken.None);

        Assert.Equal(0, llmClient.CallCount);
        Assert.Empty(db.PlaybookAmendments);
    }

    [Fact]
    public async Task ProposeAmendmentAsync_IgnoresAProposal_TargetingAnUnknownStepKey()
    {
        await using var db = CreateInMemoryDbContext();
        var scanJobId = Guid.NewGuid();
        var stepRunId = Guid.NewGuid();
        var targetStep = RemainingStep("nuclei-targeted");

        db.Assets.Add(new Asset { TargetId = Guid.NewGuid(), DiscoveredByStepRunId = stepRunId, Type = AssetType.Url, Value = "http://example.com/" });
        await db.SaveChangesAsync();

        var llmClient = new FakeLlmClient("{\"proposeAmendment\": true, \"targetStepKey\": \"does-not-exist\", \"proposedArgsTemplate\": \"-u {target}\", \"rationale\": \"x\"}");
        var planner = new AdaptivePlanner(db, llmClient, NullLogger<AdaptivePlanner>.Instance);

        await planner.ProposeAmendmentAsync(scanJobId, stepRunId, new[] { targetStep }, CancellationToken.None);

        Assert.Empty(db.PlaybookAmendments);
    }
}
