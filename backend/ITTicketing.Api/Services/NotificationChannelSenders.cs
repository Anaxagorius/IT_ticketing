using System.Net.Http.Json;
using ITTicketing.Api.Models;
using Microsoft.Extensions.Options;

namespace ITTicketing.Api.Services;

public sealed record NotificationMessage(
    NotificationChannel Channel,
    string Destination,
    string Subject,
    string Body,
    Guid TicketId,
    TicketEventType EventType);

public sealed record NotificationSendResult(bool WasSuccessful, string? FailureReason = null);

public interface INotificationChannelSender
{
    Task<NotificationSendResult> SendAsync(NotificationMessage message, CancellationToken cancellationToken);
}

public sealed class NotificationChannelSender(
    IHttpClientFactory httpClientFactory,
    IOptions<NotificationOptions> options,
    ILogger<NotificationChannelSender> logger) : INotificationChannelSender
{
    public async Task<NotificationSendResult> SendAsync(NotificationMessage message, CancellationToken cancellationToken)
    {
        var endpoint = message.Channel switch
        {
            NotificationChannel.Teams => options.Value.TeamsWebhookUrl,
            NotificationChannel.Outlook => options.Value.OutlookWebhookUrl,
            _ => null
        };

        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return new NotificationSendResult(false, $"{message.Channel} endpoint is not configured.");
        }

        try
        {
            using var client = httpClientFactory.CreateClient(nameof(NotificationChannelSender));
            object payload = message.Channel == NotificationChannel.Teams
                ? new
                {
                    title = message.Subject,
                    text = message.Body,
                    ticketId = message.TicketId,
                    eventType = message.EventType.ToString(),
                    destination = message.Destination
                }
                : new
                {
                    to = message.Destination,
                    subject = message.Subject,
                    body = message.Body,
                    ticketId = message.TicketId,
                    eventType = message.EventType.ToString()
                };

            using var response = await client.PostAsJsonAsync(endpoint, payload, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new NotificationSendResult(false, $"HTTP {(int)response.StatusCode} from {message.Channel} endpoint.");
            }

            return new NotificationSendResult(true);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Notification delivery failed for ticket {TicketId} over {Channel}", message.TicketId, message.Channel);
            return new NotificationSendResult(false, ex.Message);
        }
    }
}
