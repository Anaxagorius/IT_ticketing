namespace ITTicketing.Api.Models;

public enum NotificationChannel
{
    Teams,
    Outlook
}

public enum TicketEventType
{
    TicketCreated,
    StatusChanged,
    PriorityChanged,
    AssignmentChanged,
    EscalationTriggered,
    SlaWarning,
    SlaBreached
}

public enum SlaSignalType
{
    Warning,
    Breach
}

public sealed class NotificationRecipient
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public NotificationChannel Channel { get; init; }
    public string Destination { get; init; } = string.Empty;
    public bool IsActive { get; init; }
}

public sealed class NotificationRule
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public TicketEventType EventType { get; init; }
    public NotificationChannel Channel { get; init; }
    public int RecipientId { get; init; }
    public bool IsEnabled { get; init; }
    public TicketPriority? PriorityFilter { get; init; }
    public BranchLocation? BranchFilter { get; init; }
}

public sealed class EscalationPolicy
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public TicketPriority? PriorityFilter { get; init; }
    public BranchLocation? BranchFilter { get; init; }
    public int WarningMinutesBeforeDue { get; init; }
    public int EscalationDelayMinutes { get; init; }
    public int MaxEscalationLevel { get; init; }
    public int? ActiveFromHourUtc { get; init; }
    public int? ActiveToHourUtc { get; init; }
    public bool IsEnabled { get; init; }
}

public sealed class UpsertNotificationRecipientRequest
{
    public string Name { get; init; } = string.Empty;
    public NotificationChannel Channel { get; init; }
    public string Destination { get; init; } = string.Empty;
    public bool IsActive { get; init; } = true;
}

public sealed class UpsertNotificationRuleRequest
{
    public string Name { get; init; } = string.Empty;
    public TicketEventType EventType { get; init; }
    public NotificationChannel Channel { get; init; }
    public int RecipientId { get; init; }
    public bool IsEnabled { get; init; } = true;
    public TicketPriority? PriorityFilter { get; init; }
    public BranchLocation? BranchFilter { get; init; }
}

public sealed class UpsertEscalationPolicyRequest
{
    public string Name { get; init; } = string.Empty;
    public TicketPriority? PriorityFilter { get; init; }
    public BranchLocation? BranchFilter { get; init; }
    public int WarningMinutesBeforeDue { get; init; } = 60;
    public int EscalationDelayMinutes { get; init; } = 30;
    public int MaxEscalationLevel { get; init; } = 3;
    public int? ActiveFromHourUtc { get; init; }
    public int? ActiveToHourUtc { get; init; }
    public bool IsEnabled { get; init; } = true;
}

public sealed class ComplianceEvent
{
    public int Id { get; init; }
    public Guid TicketId { get; init; }
    public TicketEventType EventType { get; init; }
    public DateTimeOffset OccurredAt { get; init; }
    public string TriggeredBy { get; init; } = string.Empty;
    public string DetailsJson { get; init; } = "{}";
}

public sealed class ComplianceSummaryResponse
{
    public int TotalBreaches { get; init; }
    public int ActiveBreaches { get; init; }
    public int ActiveEscalations { get; init; }
    public int TotalEscalations { get; init; }
    public double MeanResolutionHours { get; init; }
    public double NotificationSuccessRate { get; init; }
    public Dictionary<string, int> BreachesByPriority { get; init; } = [];
}
