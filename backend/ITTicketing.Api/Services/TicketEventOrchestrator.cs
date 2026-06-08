using System.Text.Json;
using ITTicketing.Api.Data;
using ITTicketing.Api.Data.Entities;
using ITTicketing.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ITTicketing.Api.Services;

public sealed class TicketEventOrchestrator(TicketDbContext dbContext, INotificationChannelSender channelSender)
{
    public async Task RecordTicketEventAsync(
        TicketEntity ticket,
        TicketEventType eventType,
        string triggeredBy,
        object details,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var detailsJson = JsonSerializer.Serialize(details);

        var ticketEvent = new TicketOperationalEventEntity
        {
            TicketId = ticket.Id,
            EventType = eventType,
            TriggeredBy = triggeredBy,
            OccurredAt = now,
            DetailsJson = detailsJson
        };

        dbContext.TicketOperationalEvents.Add(ticketEvent);
        dbContext.ComplianceRecords.Add(new ComplianceRecordEntity
        {
            TicketId = ticket.Id,
            RecordType = "TicketEvent",
            CreatedAt = now,
            PayloadJson = detailsJson
        });
        await dbContext.SaveChangesAsync(cancellationToken);

        var rules = await dbContext.NotificationRules
            .AsNoTracking()
            .Include(rule => rule.Recipient)
            .Where(rule =>
                rule.IsEnabled &&
                rule.EventType == eventType &&
                (rule.PriorityFilter == null || rule.PriorityFilter == ticket.Priority) &&
                (rule.BranchFilter == null || rule.BranchFilter == ticket.Branch) &&
                rule.Recipient != null &&
                rule.Recipient.IsActive)
            .ToListAsync(cancellationToken);

        foreach (var rule in rules)
        {
            var subject = $"[{ticket.Priority}] Ticket {ticket.Title} - {eventType}";
            var body = $"Ticket {ticket.Id} at {ticket.Branch} is currently {ticket.Status}. Details: {detailsJson}";
            var result = await channelSender.SendAsync(
                new NotificationMessage(
                    rule.Channel,
                    rule.Recipient!.Destination,
                    subject,
                    body,
                    ticket.Id,
                    eventType),
                cancellationToken);

            dbContext.NotificationDeliveryAttempts.Add(new NotificationDeliveryAttemptEntity
            {
                TicketOperationalEventId = ticketEvent.Id,
                RecipientId = rule.RecipientId,
                Channel = rule.Channel,
                AttemptNumber = 1,
                AttemptedAt = now,
                WasSuccessful = result.WasSuccessful,
                FailureReason = result.FailureReason
            });

            dbContext.ComplianceRecords.Add(new ComplianceRecordEntity
            {
                TicketId = ticket.Id,
                RecordType = "NotificationDelivery",
                CreatedAt = now,
                PayloadJson = JsonSerializer.Serialize(new
                {
                    ruleId = rule.Id,
                    recipientId = rule.RecipientId,
                    channel = rule.Channel,
                    result.WasSuccessful,
                    result.FailureReason
                })
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
