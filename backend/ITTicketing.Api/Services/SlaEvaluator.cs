using ITTicketing.Api.Data;
using ITTicketing.Api.Data.Entities;
using ITTicketing.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ITTicketing.Api.Services;

public sealed class SlaEvaluator(
    TicketDbContext dbContext,
    TicketEventOrchestrator eventOrchestrator,
    EscalationEngine escalationEngine)
{
    public async Task EvaluateAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var openTickets = await dbContext.Tickets
            .Include(ticket => ticket.AuditLog)
            .Where(ticket => ticket.Status == TicketStatus.New || ticket.Status == TicketStatus.InProgress)
            .ToListAsync(cancellationToken);

        foreach (var ticket in openTickets)
        {
            var warningThresholds = await escalationEngine.GetWarningThresholdsForTicketAsync(ticket, cancellationToken);
            foreach (var threshold in warningThresholds)
            {
                var warningMoment = ticket.DueBy.AddMinutes(-threshold);
                if (now < warningMoment || now >= ticket.DueBy)
                {
                    continue;
                }

                var warningAlreadyRaised = await dbContext.SlaSignals
                    .AnyAsync(signal =>
                        signal.TicketId == ticket.Id &&
                        signal.SignalType == SlaSignalType.Warning &&
                        signal.ThresholdMinutes == threshold,
                        cancellationToken);

                if (warningAlreadyRaised)
                {
                    continue;
                }

                var signal = new SlaSignalEntity
                {
                    TicketId = ticket.Id,
                    SignalType = SlaSignalType.Warning,
                    ThresholdMinutes = threshold,
                    TriggeredAt = now
                };
                dbContext.SlaSignals.Add(signal);
                ticket.AuditLog.Add(new TicketAuditEntryEntity
                {
                    Action = $"SLA warning triggered ({threshold} minutes before due)",
                    PerformedBy = "SLA Monitor",
                    Timestamp = now
                });
                await dbContext.SaveChangesAsync(cancellationToken);
                await eventOrchestrator.RecordTicketEventAsync(
                    ticket,
                    TicketEventType.SlaWarning,
                    "SLA Monitor",
                    new { thresholdMinutes = threshold, ticket.DueBy },
                    cancellationToken);
            }

            if (now < ticket.DueBy)
            {
                continue;
            }

            var breachAlreadyRaised = await dbContext.SlaSignals
                .AnyAsync(signal =>
                    signal.TicketId == ticket.Id &&
                    signal.SignalType == SlaSignalType.Breach,
                    cancellationToken);

            if (breachAlreadyRaised)
            {
                continue;
            }

            dbContext.SlaSignals.Add(new SlaSignalEntity
            {
                TicketId = ticket.Id,
                SignalType = SlaSignalType.Breach,
                TriggeredAt = now
            });
            ticket.AuditLog.Add(new TicketAuditEntryEntity
            {
                Action = "SLA breached",
                PerformedBy = "SLA Monitor",
                Timestamp = now
            });

            await dbContext.SaveChangesAsync(cancellationToken);
            await eventOrchestrator.RecordTicketEventAsync(
                ticket,
                TicketEventType.SlaBreached,
                "SLA Monitor",
                new { ticket.DueBy, breachedAt = now },
                cancellationToken);
        }

        await escalationEngine.EvaluateEscalationsAsync(openTickets.Where(ticket => now >= ticket.DueBy), cancellationToken);
    }
}
