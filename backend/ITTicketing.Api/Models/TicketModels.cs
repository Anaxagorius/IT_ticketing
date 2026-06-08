namespace ITTicketing.Api.Models;

public enum BranchLocation
{
    HeadOffice,
    Branch1,
    Branch2,
    Branch3,
    Branch4,
    Branch5,
    Branch6
}

public enum TicketPriority
{
    Low,
    Medium,
    High,
    Critical
}

public enum TicketStatus
{
    New,
    InProgress,
    Resolved,
    Closed
}

public sealed class Ticket
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public BranchLocation Branch { get; set; }
    public TicketPriority Priority { get; set; }
    public TicketStatus Status { get; set; } = TicketStatus.New;
    public string SubmittedBy { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset DueBy { get; set; }
    public List<TicketAuditEntry> AuditLog { get; set; } = [];
}

public sealed class TicketAuditEntry
{
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public string Action { get; init; } = string.Empty;
    public string PerformedBy { get; init; } = string.Empty;
}

public sealed class CreateTicketRequest
{
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public BranchLocation Branch { get; init; }
    public TicketPriority Priority { get; init; }
    public string SubmittedBy { get; init; } = string.Empty;
}

public sealed class UpdateTicketStatusRequest
{
    public TicketStatus Status { get; init; }
    public string UpdatedBy { get; init; } = string.Empty;
}

public sealed class TicketSummaryResponse
{
    public int TotalOpen { get; init; }
    public int TotalResolved { get; init; }
    public Dictionary<string, int> ByBranch { get; init; } = [];
    public Dictionary<string, int> ByPriority { get; init; } = [];
}
