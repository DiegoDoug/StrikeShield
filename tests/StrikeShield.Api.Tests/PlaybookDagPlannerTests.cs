using StrikeShield.Domain.Entities;
using StrikeShield.Domain.Enums;
using StrikeShield.Orchestrator;
using Xunit;

namespace StrikeShield.Api.Tests;

/// <summary>
/// Phase 5 acceptance test (docs/PHASED_PLAN.md): a DAG's steps must
/// execute in dependency order, not just Order-field sequence, and
/// existing linear playbooks (no DependsOn at all) must keep behaving
/// exactly as they did in Phases 2-4.
/// </summary>
public class PlaybookDagPlannerTests
{
    private static PlaybookStep Step(string key, int order, params string[] dependsOn) => new()
    {
        StepKey = key,
        Order = order,
        ToolName = key,
        ImageRepository = "test/" + key,
        ImageTag = "latest",
        ArgsTemplate = "--noop",
        DependsOn = dependsOn.ToList()
    };

    [Fact]
    public void TopologicalOrder_PreservesOrderField_WhenNoStepDeclaresDependencies()
    {
        var steps = new List<PlaybookStep> { Step("nuclei", 1), Step("zap", 2), Step("nmap", 3) };

        var ordered = PlaybookDagPlanner.TopologicalOrder(steps);

        Assert.Equal(new[] { "nuclei", "zap", "nmap" }, ordered.Select(s => s.StepKey));
    }

    [Fact]
    public void TopologicalOrder_RunsDependencyBeforeDependent_EvenWhenOrderFieldSaysOtherwise()
    {
        // "nmap-recon" is declared first (lower Order) but depends on
        // "subfinder", which is declared later — DependsOn must win.
        var steps = new List<PlaybookStep>
        {
            Step("nmap-recon", 1, "subfinder"),
            Step("subfinder", 2)
        };

        var ordered = PlaybookDagPlanner.TopologicalOrder(steps);

        Assert.Equal(new[] { "subfinder", "nmap-recon" }, ordered.Select(s => s.StepKey));
    }

    [Fact]
    public void TopologicalOrder_RunsIndependentBranchesByOrder_AroundASharedDependency()
    {
        var steps = new List<PlaybookStep>
        {
            Step("subfinder", 1),
            Step("katana", 2),
            Step("nmap-recon", 3, "subfinder"),
            Step("ffuf", 4, "katana"),
            Step("nuclei-targeted", 5, "katana"),
            Step("nikto", 6)
        };

        var ordered = PlaybookDagPlanner.TopologicalOrder(steps).Select(s => s.StepKey).ToList();

        Assert.True(ordered.IndexOf("subfinder") < ordered.IndexOf("nmap-recon"));
        Assert.True(ordered.IndexOf("katana") < ordered.IndexOf("ffuf"));
        Assert.True(ordered.IndexOf("katana") < ordered.IndexOf("nuclei-targeted"));
        Assert.Equal(6, ordered.Count);
    }

    [Fact]
    public void TopologicalOrder_Throws_WhenDependsOnNamesAnUnknownStepKey()
    {
        var steps = new List<PlaybookStep> { Step("nmap-recon", 1, "does-not-exist") };

        Assert.Throws<InvalidOperationException>(() => PlaybookDagPlanner.TopologicalOrder(steps));
    }

    [Fact]
    public void TopologicalOrder_Throws_OnACycle()
    {
        var steps = new List<PlaybookStep> { Step("a", 1, "b"), Step("b", 2, "a") };

        Assert.Throws<InvalidOperationException>(() => PlaybookDagPlanner.TopologicalOrder(steps));
    }

    [Fact]
    public void TopologicalOrder_Throws_OnDuplicateStepKeyWithinOnePlaybook()
    {
        var steps = new List<PlaybookStep> { Step("nuclei", 1), Step("nuclei", 2) };

        Assert.Throws<InvalidOperationException>(() => PlaybookDagPlanner.TopologicalOrder(steps));
    }
}
