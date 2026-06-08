namespace ITTicketing.Api.Data.Entities;

public sealed class TicketAuditEntryEntity
{
    public int Id { get; set; }
    public Guid TicketId { get; set; }
    public TicketEntity? Ticket { get; set; }
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public string Action { get; set; } = string.Empty;
    public string PerformedBy { get; set; } = string.Empty;
}
