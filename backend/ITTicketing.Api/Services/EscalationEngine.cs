using ITTicketing.Api.Data;
using ITTicketing.Api.Data.Entities;
using ITTicketing.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ITTicketing.Api.Services;

public sealed class EscalationEngine(TicketDbContext dbContext, TicketEventOrchestrator eventOrchestrator)
{
    public async Task EvaluateEscalationsAsync(IEnumerable<TicketEntity> tickets, CancellationToken cancellationToken = default)
    {
        var policies = await dbContext.EscalationPolicies
            .AsNoTracking()
            .Where(policy => policy.IsEnabled)
            .ToListAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;

        foreach (var ticket in tickets)
        {
            foreach (var policy in policies.Where(policy => IsPolicyApplicable(policy, ticket, now)))
            {
                var existingActions = await dbContext.EscalationActions
                    .Where(action => action.TicketId == ticket.Id && action.EscalationPolicyId == policy.Id)
                    .OrderByDescending(action => action.EscalationLevel)
                    .ToListAsync(cancellationToken);

                var currentLevel = existingActions.FirstOrDefault()?.EscalationLevel ?? 0;
                if (currentLevel >= policy.MaxEscalationLevel)
                {
                    continue;
                }

                if (currentLevel > 0)
                {
                    var latestAction = existingActions.First();
                    if (latestAction.TriggeredAt.AddMinutes(policy.EscalationDelayMinutes) > now)
                    {
                        continue;
                    }
                }

                var nextLevel = currentLevel + 1;
                dbContext.EscalationActions.Add(new EscalationActionEntity
                {
                    TicketId = ticket.Id,
                    EscalationPolicyId = policy.Id,
                    EscalationLevel = nextLevel,
                    Reason = $"Escalation policy '{policy.Name}' triggered level {nextLevel}",
                    TriggeredAt = now
                });
                ticket.UpdatedAt = now;
                ticket.AuditLog.Add(new TicketAuditEntryEntity
                {
                    Action = $"Escalated to level {nextLevel} by policy {policy.Name}",
                    PerformedBy = "Escalation Engine",
                    Timestamp = now
                });

                await dbContext.SaveChangesAsync(cancellationToken);
                await eventOrchestrator.RecordTicketEventAsync(
                    ticket,
                    TicketEventType.EscalationTriggered,
                    "Escalation Engine",
                    new { policy = policy.Name, level = nextLevel },
                    cancellationToken);
            }
        }
    }

    public async Task<List<int>> GetWarningThresholdsForTicketAsync(TicketEntity ticket, CancellationToken cancellationToken = default)
    {
        var policyThresholds = await dbContext.EscalationPolicies
            .AsNoTracking()
            .Where(policy =>
                policy.IsEnabled &&
                (policy.PriorityFilter == null || policy.PriorityFilter == ticket.Priority) &&
                (policy.BranchFilter == null || policy.BranchFilter == ticket.Branch))
            .Select(policy => policy.WarningMinutesBeforeDue)
            .ToListAsync(cancellationToken);

        return policyThresholds
            .Append(60)
            .Append(30)
            .Where(value => value > 0)
            .Distinct()
            .OrderByDescending(value => value)
            .ToList();
    }

    private static bool IsPolicyApplicable(EscalationPolicyEntity policy, TicketEntity ticket, DateTimeOffset now)
    {
        if (policy.PriorityFilter != null && policy.PriorityFilter != ticket.Priority)
        {
            return false;
        }

        if (policy.BranchFilter != null && policy.BranchFilter != ticket.Branch)
        {
            return false;
        }

        if (policy.ActiveFromHourUtc == null || policy.ActiveToHourUtc == null)
        {
            return true;
        }

        var hour = now.Hour;
        var start = policy.ActiveFromHourUtc.Value;
        var end = policy.ActiveToHourUtc.Value;

        return start <= end
            ? hour >= start && hour < end
            : hour >= start || hour < end;
    }
}
