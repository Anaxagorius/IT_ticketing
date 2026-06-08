using ITTicketing.Api.Data;
using ITTicketing.Api.Data.Entities;
using ITTicketing.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ITTicketing.Api.Services;

public sealed class SlaMonitoringService(
    IServiceScopeFactory scopeFactory,
    IOptions<SlaMonitoringOptions> options,
    ILogger<SlaMonitoringService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalSeconds = Math.Max(30, options.Value.IntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await EvaluateAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "SLA monitoring iteration failed");
            }

            await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken);
        }
    }

    private async Task EvaluateAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TicketDbContext>();
        var eventOrchestrator = scope.ServiceProvider.GetRequiredService<TicketEventOrchestrator>();
        var escalationEngine = scope.ServiceProvider.GetRequiredService<EscalationEngine>();

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
