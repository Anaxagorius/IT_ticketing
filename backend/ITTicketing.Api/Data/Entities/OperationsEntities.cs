using ITTicketing.Api.Models;

namespace ITTicketing.Api.Data.Entities;

public sealed class NotificationRecipientEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public NotificationChannel Channel { get; set; }
    public string Destination { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<NotificationRuleEntity> NotificationRules { get; set; } = [];
    public List<NotificationDeliveryAttemptEntity> DeliveryAttempts { get; set; } = [];
}

public sealed class NotificationRuleEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public TicketEventType EventType { get; set; }
    public NotificationChannel Channel { get; set; }
    public int RecipientId { get; set; }
    public NotificationRecipientEntity? Recipient { get; set; }
    public bool IsEnabled { get; set; } = true;
    public TicketPriority? PriorityFilter { get; set; }
    public BranchLocation? BranchFilter { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class EscalationPolicyEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public TicketPriority? PriorityFilter { get; set; }
    public BranchLocation? BranchFilter { get; set; }
    public int WarningMinutesBeforeDue { get; set; } = 60;
    public int EscalationDelayMinutes { get; set; } = 30;
    public int MaxEscalationLevel { get; set; } = 3;
    public int? ActiveFromHourUtc { get; set; }
    public int? ActiveToHourUtc { get; set; }
    public bool IsEnabled { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class TicketOperationalEventEntity
{
    public int Id { get; set; }
    public Guid TicketId { get; set; }
    public TicketEntity? Ticket { get; set; }
    public TicketEventType EventType { get; set; }
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
    public string TriggeredBy { get; set; } = string.Empty;
    public string DetailsJson { get; set; } = "{}";
    public List<NotificationDeliveryAttemptEntity> DeliveryAttempts { get; set; } = [];
}

public sealed class NotificationDeliveryAttemptEntity
{
    public int Id { get; set; }
    public int TicketOperationalEventId { get; set; }
    public TicketOperationalEventEntity? TicketOperationalEvent { get; set; }
    public int RecipientId { get; set; }
    public NotificationRecipientEntity? Recipient { get; set; }
    public NotificationChannel Channel { get; set; }
    public int AttemptNumber { get; set; } = 1;
    public DateTimeOffset AttemptedAt { get; set; } = DateTimeOffset.UtcNow;
    public bool WasSuccessful { get; set; }
    public string? FailureReason { get; set; }
}

public sealed class SlaSignalEntity
{
    public int Id { get; set; }
    public Guid TicketId { get; set; }
    public TicketEntity? Ticket { get; set; }
    public SlaSignalType SignalType { get; set; }
    public int? ThresholdMinutes { get; set; }
    public DateTimeOffset TriggeredAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class EscalationActionEntity
{
    public int Id { get; set; }
    public Guid TicketId { get; set; }
    public TicketEntity? Ticket { get; set; }
    public int EscalationPolicyId { get; set; }
    public EscalationPolicyEntity? EscalationPolicy { get; set; }
    public int EscalationLevel { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTimeOffset TriggeredAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class ComplianceRecordEntity
{
    public long Id { get; set; }
    public Guid TicketId { get; set; }
    public TicketEntity? Ticket { get; set; }
    public string RecordType { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string PayloadJson { get; set; } = "{}";
}
