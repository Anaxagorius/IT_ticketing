using ITTicketing.Api.Models;

namespace ITTicketing.Api.Data.Entities;

public sealed class TicketEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public BranchLocation Branch { get; set; }
    public TicketPriority Priority { get; set; }
    public TicketStatus Status { get; set; } = TicketStatus.New;
    public string SubmittedBy { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset DueBy { get; set; }
    public List<TicketAuditEntryEntity> AuditLog { get; set; } = [];
}
