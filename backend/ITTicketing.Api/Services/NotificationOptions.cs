namespace ITTicketing.Api.Services;

public sealed class NotificationOptions
{
    public const string SectionName = "Notifications";
    public string? TeamsWebhookUrl { get; init; }
    public string? OutlookWebhookUrl { get; init; }
}

public sealed class SlaMonitoringOptions
{
    public const string SectionName = "SlaMonitoring";
    public int IntervalSeconds { get; init; } = 60;
    public int[] DefaultWarningMinutesBeforeDue { get; init; } = [60, 30];
}
