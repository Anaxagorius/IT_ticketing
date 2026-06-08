using ITTicketing.Api.Data;
using ITTicketing.Api.Data.Entities;
using ITTicketing.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ITTicketing.Api.Services;

public sealed class TicketStore(TicketDbContext dbContext, TicketEventOrchestrator eventOrchestrator)
{
    public IEnumerable<Ticket> GetTickets(BranchLocation? branch, TicketPriority? priority, TicketStatus? status)
    {
        var query = dbContext.Tickets
            .AsNoTracking()
            .Include(ticket => ticket.AuditLog)
            .AsQueryable();

        if (branch.HasValue)
        {
            query = query.Where(ticket => ticket.Branch == branch.Value);
        }

        if (priority.HasValue)
        {
            query = query.Where(ticket => ticket.Priority == priority.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(ticket => ticket.Status == status.Value);
        }

        return query
            .OrderByDescending(ticket => ticket.CreatedAt)
            .ToList()
            .Select(ToModel);
    }

    public Ticket? GetTicket(Guid id)
    {
        var ticket = dbContext.Tickets
            .AsNoTracking()
            .Include(item => item.AuditLog)
            .FirstOrDefault(item => item.Id == id);

        return ticket is null ? null : ToModel(ticket);
    }

    public async Task<Ticket> CreateAsync(CreateTicketRequest request, CancellationToken cancellationToken = default)
    {
        var normalizedSubmitter = string.IsNullOrWhiteSpace(request.SubmittedBy) ? "Employee" : request.SubmittedBy.Trim();
        var now = DateTimeOffset.UtcNow;

        var ticket = new TicketEntity
        {
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            Branch = request.Branch,
            Priority = request.Priority,
            SubmittedBy = normalizedSubmitter,
            DueBy = now.Add(GetSlaWindow(request.Priority)),
            AuditLog =
            [
                new TicketAuditEntryEntity
                {
                    Action = "Ticket created",
                    PerformedBy = normalizedSubmitter,
                    Timestamp = now
                }
            ]
        };

        dbContext.Tickets.Add(ticket);
        await dbContext.SaveChangesAsync(cancellationToken);
        await eventOrchestrator.RecordTicketEventAsync(
            ticket,
            TicketEventType.TicketCreated,
            normalizedSubmitter,
            new { ticket.Title, ticket.Priority, ticket.Branch, ticket.DueBy },
            cancellationToken);

        return ToModel(ticket);
    }

    public async Task<Ticket?> UpdateStatusAsync(Guid id, UpdateTicketStatusRequest request, CancellationToken cancellationToken = default)
    {
        var ticket = await dbContext.Tickets
            .Include(item => item.AuditLog)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (ticket is null)
        {
            return null;
        }

        var previousStatus = ticket.Status;
        var updatedBy = string.IsNullOrWhiteSpace(request.UpdatedBy) ? "IT Staff" : request.UpdatedBy.Trim();
        var now = DateTimeOffset.UtcNow;

        ticket.Status = request.Status;
        ticket.UpdatedAt = now;
        ticket.AuditLog.Add(new TicketAuditEntryEntity
        {
            Action = $"Status updated from {previousStatus} to {request.Status}",
            PerformedBy = updatedBy,
            Timestamp = now
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        await eventOrchestrator.RecordTicketEventAsync(
            ticket,
            TicketEventType.StatusChanged,
            updatedBy,
            new { previousStatus, currentStatus = ticket.Status },
            cancellationToken);

        return ToModel(ticket);
    }

    public async Task<Ticket?> UpdateDetailsAsync(Guid id, UpdateTicketDetailsRequest request, CancellationToken cancellationToken = default)
    {
        var ticket = await dbContext.Tickets
            .Include(item => item.AuditLog)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (ticket is null)
        {
            return null;
        }

        var updatedBy = string.IsNullOrWhiteSpace(request.UpdatedBy) ? "IT Staff" : request.UpdatedBy.Trim();
        var now = DateTimeOffset.UtcNow;
        var hasChanges = false;

        if (request.Priority.HasValue && request.Priority.Value != ticket.Priority)
        {
            var previousPriority = ticket.Priority;
            ticket.Priority = request.Priority.Value;
            ticket.DueBy = now.Add(GetSlaWindow(ticket.Priority));
            ticket.AuditLog.Add(new TicketAuditEntryEntity
            {
                Action = $"Priority updated from {previousPriority} to {ticket.Priority}",
                PerformedBy = updatedBy,
                Timestamp = now
            });
            await eventOrchestrator.RecordTicketEventAsync(
                ticket,
                TicketEventType.PriorityChanged,
                updatedBy,
                new { previousPriority, currentPriority = ticket.Priority, ticket.DueBy },
                cancellationToken);
            hasChanges = true;
        }

        if (request.AssignedTo != null)
        {
            var normalizedAssignedTo = string.IsNullOrWhiteSpace(request.AssignedTo) ? null : request.AssignedTo.Trim();
            if (!string.Equals(ticket.AssignedTo, normalizedAssignedTo, StringComparison.Ordinal))
            {
                var previousAssignee = ticket.AssignedTo;
                ticket.AssignedTo = normalizedAssignedTo;
                ticket.AuditLog.Add(new TicketAuditEntryEntity
                {
                    Action = $"Assignment updated from '{previousAssignee ?? "Unassigned"}' to '{ticket.AssignedTo ?? "Unassigned"}'",
                    PerformedBy = updatedBy,
                    Timestamp = now
                });
                await eventOrchestrator.RecordTicketEventAsync(
                    ticket,
                    TicketEventType.AssignmentChanged,
                    updatedBy,
                    new { previousAssignee, currentAssignee = ticket.AssignedTo },
                    cancellationToken);
                hasChanges = true;
            }
        }

        if (!hasChanges)
        {
            return ToModel(ticket);
        }

        ticket.UpdatedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToModel(ticket);
    }

    public TicketSummaryResponse GetSummary()
    {
        var tickets = dbContext.Tickets
            .AsNoTracking()
            .ToList();
        var openStatuses = new[] { TicketStatus.New, TicketStatus.InProgress };
        var resolvedStatuses = new[] { TicketStatus.Resolved, TicketStatus.Closed };

        var byBranch = tickets
            .GroupBy(ticket => ticket.Branch)
            .Select(group => new { group.Key, Count = group.Count() })
            .ToDictionary(item => item.Key.ToString(), item => item.Count);

        var byPriority = tickets
            .GroupBy(ticket => ticket.Priority)
            .Select(group => new { group.Key, Count = group.Count() })
            .ToDictionary(item => item.Key.ToString(), item => item.Count);

        return new TicketSummaryResponse
        {
            TotalOpen = tickets.Count(ticket => openStatuses.Contains(ticket.Status)),
            TotalResolved = tickets.Count(ticket => resolvedStatuses.Contains(ticket.Status)),
            ByBranch = byBranch,
            ByPriority = byPriority
        };
    }

    private static TimeSpan GetSlaWindow(TicketPriority priority) =>
        priority switch
        {
            TicketPriority.Critical => TimeSpan.FromHours(4),
            TicketPriority.High => TimeSpan.FromHours(8),
            TicketPriority.Medium => TimeSpan.FromHours(24),
            _ => TimeSpan.FromHours(48)
        };

    private static Ticket ToModel(TicketEntity ticketEntity) =>
        new()
        {
            Id = ticketEntity.Id,
            Title = ticketEntity.Title,
            Description = ticketEntity.Description,
            Branch = ticketEntity.Branch,
            Priority = ticketEntity.Priority,
            Status = ticketEntity.Status,
            SubmittedBy = ticketEntity.SubmittedBy,
            AssignedTo = ticketEntity.AssignedTo,
            CreatedAt = ticketEntity.CreatedAt,
            UpdatedAt = ticketEntity.UpdatedAt,
            DueBy = ticketEntity.DueBy,
            AuditLog = ticketEntity.AuditLog
                .OrderBy(entry => entry.Timestamp)
                .Select(entry => new TicketAuditEntry
                {
                    Timestamp = entry.Timestamp,
                    Action = entry.Action,
                    PerformedBy = entry.PerformedBy
                })
                .ToList()
        };
}
