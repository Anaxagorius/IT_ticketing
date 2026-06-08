using System.Text;
using ITTicketing.Api.Data;
using ITTicketing.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ITTicketing.Api.Controllers;

[ApiController]
[Route("api/compliance")]
public sealed class ComplianceController(TicketDbContext dbContext) : ControllerBase
{
    [HttpGet("summary")]
    public async Task<ActionResult<ComplianceSummaryResponse>> GetSummary(CancellationToken cancellationToken)
    {
        var breaches = await dbContext.SlaSignals
            .AsNoTracking()
            .Where(signal => signal.SignalType == SlaSignalType.Breach)
            .ToListAsync(cancellationToken);

        var openTicketIds = await dbContext.Tickets
            .AsNoTracking()
            .Where(ticket => ticket.Status == TicketStatus.New || ticket.Status == TicketStatus.InProgress)
            .Select(ticket => ticket.Id)
            .ToHashSetAsync(cancellationToken);

        var activeBreaches = breaches.Count(signal => openTicketIds.Contains(signal.TicketId));

        var escalations = await dbContext.EscalationActions
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var activeEscalations = escalations.Count(action => openTicketIds.Contains(action.TicketId));

        var resolvedTickets = await dbContext.Tickets
            .AsNoTracking()
            .Where(ticket => ticket.Status == TicketStatus.Resolved || ticket.Status == TicketStatus.Closed)
            .ToListAsync(cancellationToken);

        var meanResolutionHours = resolvedTickets.Count == 0
            ? 0
            : resolvedTickets.Average(ticket => (ticket.UpdatedAt - ticket.CreatedAt).TotalHours);

        var attempts = await dbContext.NotificationDeliveryAttempts
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        var successRate = attempts.Count == 0 ? 0 : attempts.Count(attempt => attempt.WasSuccessful) / (double)attempts.Count;

        var breachesByPriority = await dbContext.Tickets
            .AsNoTracking()
            .Where(ticket => breaches.Select(signal => signal.TicketId).Contains(ticket.Id))
            .GroupBy(ticket => ticket.Priority)
            .Select(group => new { group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.Key.ToString(), item => item.Count, cancellationToken);

        return Ok(new ComplianceSummaryResponse
        {
            TotalBreaches = breaches.Count,
            ActiveBreaches = activeBreaches,
            ActiveEscalations = activeEscalations,
            TotalEscalations = escalations.Count,
            MeanResolutionHours = Math.Round(meanResolutionHours, 2),
            NotificationSuccessRate = Math.Round(successRate, 3),
            BreachesByPriority = breachesByPriority
        });
    }

    [HttpGet("tickets/{ticketId:guid}/events")]
    public async Task<ActionResult<IEnumerable<ComplianceEvent>>> GetTicketEvents(Guid ticketId, CancellationToken cancellationToken)
    {
        var exists = await dbContext.Tickets.AsNoTracking().AnyAsync(ticket => ticket.Id == ticketId, cancellationToken);
        if (!exists)
        {
            return NotFound();
        }

        var events = await dbContext.TicketOperationalEvents
            .AsNoTracking()
            .Where(item => item.TicketId == ticketId)
            .OrderBy(item => item.OccurredAt)
            .Select(item => new ComplianceEvent
            {
                Id = item.Id,
                TicketId = item.TicketId,
                EventType = item.EventType,
                OccurredAt = item.OccurredAt,
                TriggeredBy = item.TriggeredBy,
                DetailsJson = item.DetailsJson
            })
            .ToListAsync(cancellationToken);

        return Ok(events);
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] string format = "json", CancellationToken cancellationToken = default)
    {
        var records = await dbContext.ComplianceRecords
            .AsNoTracking()
            .OrderBy(item => item.CreatedAt)
            .Select(item => new
            {
                item.Id,
                item.TicketId,
                item.RecordType,
                item.CreatedAt,
                item.PayloadJson
            })
            .ToListAsync(cancellationToken);

        if (string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase))
        {
            var builder = new StringBuilder();
            builder.AppendLine("Id,TicketId,RecordType,CreatedAt,PayloadJson");
            foreach (var record in records)
            {
                builder.AppendLine($"{record.Id},{record.TicketId},{Escape(record.RecordType)},{record.CreatedAt:O},{Escape(record.PayloadJson)}");
            }

            return File(Encoding.UTF8.GetBytes(builder.ToString()), "text/csv", "compliance-export.csv");
        }

        return Ok(records);
    }

    private static string Escape(string value) => $"\"{value.Replace("\"", "\"\"")}\"";
}
