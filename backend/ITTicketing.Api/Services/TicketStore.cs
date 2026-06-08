using ITTicketing.Api.Models;

namespace ITTicketing.Api.Services;

public sealed class TicketStore
{
    private readonly List<Ticket> _tickets = [];
    private readonly object _sync = new();

    public IEnumerable<Ticket> GetTickets(BranchLocation? branch, TicketPriority? priority, TicketStatus? status)
    {
        lock (_sync)
        {
            return _tickets
                .Where(t => !branch.HasValue || t.Branch == branch.Value)
                .Where(t => !priority.HasValue || t.Priority == priority.Value)
                .Where(t => !status.HasValue || t.Status == status.Value)
                .OrderByDescending(t => t.CreatedAt)
                .Select(Clone)
                .ToList();
        }
    }

    public Ticket? GetTicket(Guid id)
    {
        lock (_sync)
        {
            var ticket = _tickets.FirstOrDefault(t => t.Id == id);
            return ticket is null ? null : Clone(ticket);
        }
    }

    public Ticket Create(CreateTicketRequest request)
    {
        var normalizedSubmitter = string.IsNullOrWhiteSpace(request.SubmittedBy) ? "Employee" : request.SubmittedBy.Trim();
        var now = DateTimeOffset.UtcNow;

        var ticket = new Ticket
        {
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            Branch = request.Branch,
            Priority = request.Priority,
            SubmittedBy = normalizedSubmitter,
            DueBy = now.Add(GetSlaWindow(request.Priority)),
            AuditLog =
            [
                new TicketAuditEntry
                {
                    Action = "Ticket created",
                    PerformedBy = normalizedSubmitter,
                    Timestamp = now
                }
            ]
        };

        lock (_sync)
        {
            _tickets.Add(ticket);
        }

        return Clone(ticket);
    }

    public Ticket? UpdateStatus(Guid id, UpdateTicketStatusRequest request)
    {
        lock (_sync)
        {
            var ticket = _tickets.FirstOrDefault(t => t.Id == id);
            if (ticket is null)
            {
                return null;
            }

            var updatedBy = string.IsNullOrWhiteSpace(request.UpdatedBy) ? "IT Staff" : request.UpdatedBy.Trim();
            ticket.Status = request.Status;
            ticket.UpdatedAt = DateTimeOffset.UtcNow;
            ticket.AuditLog.Add(new TicketAuditEntry
            {
                Action = $"Status updated to {request.Status}",
                PerformedBy = updatedBy,
                Timestamp = ticket.UpdatedAt
            });

            return Clone(ticket);
        }
    }

    public TicketSummaryResponse GetSummary()
    {
        lock (_sync)
        {
            var openStatuses = new[] { TicketStatus.New, TicketStatus.InProgress };
            return new TicketSummaryResponse
            {
                TotalOpen = _tickets.Count(t => openStatuses.Contains(t.Status)),
                TotalResolved = _tickets.Count(t => t.Status is TicketStatus.Resolved or TicketStatus.Closed),
                ByBranch = _tickets
                    .GroupBy(t => t.Branch.ToString())
                    .ToDictionary(group => group.Key, group => group.Count()),
                ByPriority = _tickets
                    .GroupBy(t => t.Priority.ToString())
                    .ToDictionary(group => group.Key, group => group.Count())
            };
        }
    }

    private static TimeSpan GetSlaWindow(TicketPriority priority) =>
        priority switch
        {
            TicketPriority.Critical => TimeSpan.FromHours(4),
            TicketPriority.High => TimeSpan.FromHours(8),
            TicketPriority.Medium => TimeSpan.FromHours(24),
            _ => TimeSpan.FromHours(48)
        };

    private static Ticket Clone(Ticket ticket) =>
        new()
        {
            Id = ticket.Id,
            Title = ticket.Title,
            Description = ticket.Description,
            Branch = ticket.Branch,
            Priority = ticket.Priority,
            Status = ticket.Status,
            SubmittedBy = ticket.SubmittedBy,
            CreatedAt = ticket.CreatedAt,
            UpdatedAt = ticket.UpdatedAt,
            DueBy = ticket.DueBy,
            AuditLog = ticket.AuditLog
                .Select(entry => new TicketAuditEntry
                {
                    Timestamp = entry.Timestamp,
                    Action = entry.Action,
                    PerformedBy = entry.PerformedBy
                })
                .ToList()
        };
}
