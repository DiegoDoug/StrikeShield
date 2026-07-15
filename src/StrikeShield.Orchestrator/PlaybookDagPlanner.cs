using StrikeShield.Domain.Entities;

namespace StrikeShield.Orchestrator;

/// <summary>
/// Resolves a Playbook's steps into a valid execution order given their
/// DependsOn (StepKey) declarations (docs/PHASED_PLAN.md Phase 5). Pure and
/// Docker-free by design so it's directly unit-testable without a Docker
/// daemon — see PlaybookDagPlannerTests.
/// </summary>
public static class PlaybookDagPlanner
{
    /// <summary>
    /// Depth-first topological sort: a step's dependencies always precede
    /// it in the returned list. Steps with no DependsOn at all (every
    /// Phase 2-4 playbook) are visited in (Order, StepKey) sequence, so
    /// existing linear playbooks execute in exactly the order they always
    /// have. Throws if a StepKey is duplicated, a DependsOn entry names an
    /// unknown step, or the graph contains a cycle.
    /// </summary>
    public static List<PlaybookStep> TopologicalOrder(IReadOnlyList<PlaybookStep> steps)
    {
        var byKey = new Dictionary<string, PlaybookStep>(StringComparer.OrdinalIgnoreCase);
        foreach (var step in steps)
        {
            if (string.IsNullOrWhiteSpace(step.StepKey))
            {
                throw new InvalidOperationException($"PlaybookStep {step.Id} has no StepKey.");
            }

            if (!byKey.TryAdd(step.StepKey, step))
            {
                throw new InvalidOperationException($"Duplicate StepKey '{step.StepKey}' within the same playbook.");
            }
        }

        foreach (var step in steps)
        {
            foreach (var dependencyKey in step.DependsOn)
            {
                if (!byKey.ContainsKey(dependencyKey))
                {
                    throw new InvalidOperationException(
                        $"Step '{step.StepKey}' depends on unknown step key '{dependencyKey}'.");
                }
            }
        }

        var visitState = new Dictionary<string, VisitState>(StringComparer.OrdinalIgnoreCase);
        var result = new List<PlaybookStep>();

        foreach (var step in steps.OrderBy(s => s.Order).ThenBy(s => s.StepKey, StringComparer.OrdinalIgnoreCase))
        {
            Visit(step, byKey, visitState, result);
        }

        return result;
    }

    private enum VisitState
    {
        InProgress,
        Done
    }

    private static void Visit(
        PlaybookStep step,
        IReadOnlyDictionary<string, PlaybookStep> byKey,
        Dictionary<string, VisitState> visitState,
        List<PlaybookStep> result)
    {
        if (visitState.TryGetValue(step.StepKey, out var state))
        {
            if (state == VisitState.InProgress)
            {
                throw new InvalidOperationException($"Cycle detected in playbook steps involving '{step.StepKey}'.");
            }

            return;
        }

        visitState[step.StepKey] = VisitState.InProgress;

        foreach (var dependencyKey in step.DependsOn.OrderBy(k => k, StringComparer.OrdinalIgnoreCase))
        {
            Visit(byKey[dependencyKey], byKey, visitState, result);
        }

        visitState[step.StepKey] = VisitState.Done;
        result.Add(step);
    }
}
