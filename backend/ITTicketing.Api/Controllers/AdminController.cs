using ITTicketing.Api.Data;
using ITTicketing.Api.Data.Entities;
using ITTicketing.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ITTicketing.Api.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Policy = "Admin")]
public sealed class AdminController(TicketDbContext dbContext) : ControllerBase
{
    [HttpGet("recipients")]
    public async Task<ActionResult<IEnumerable<NotificationRecipient>>> GetRecipients(CancellationToken cancellationToken)
    {
        var recipients = await dbContext.NotificationRecipients
            .AsNoTracking()
            .OrderBy(item => item.Name)
            .Select(item => new NotificationRecipient
            {
                Id = item.Id,
                Name = item.Name,
                Channel = item.Channel,
                Destination = item.Destination,
                IsActive = item.IsActive
            })
            .ToListAsync(cancellationToken);

        return Ok(recipients);
    }

    [HttpPost("recipients")]
    public async Task<ActionResult<NotificationRecipient>> CreateRecipient([FromBody] UpsertNotificationRecipientRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Destination))
        {
            return ValidationProblem("Recipient name and destination are required.");
        }

        var now = DateTimeOffset.UtcNow;
        var entity = new NotificationRecipientEntity
        {
            Name = request.Name.Trim(),
            Channel = request.Channel,
            Destination = request.Destination.Trim(),
            IsActive = request.IsActive,
            CreatedAt = now,
            UpdatedAt = now
        };

        dbContext.NotificationRecipients.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetRecipients), new { id = entity.Id }, new NotificationRecipient
        {
            Id = entity.Id,
            Name = entity.Name,
            Channel = entity.Channel,
            Destination = entity.Destination,
            IsActive = entity.IsActive
        });
    }

    [HttpPut("recipients/{id:int}")]
    public async Task<ActionResult<NotificationRecipient>> UpdateRecipient(int id, [FromBody] UpsertNotificationRecipientRequest request, CancellationToken cancellationToken)
    {
        var entity = await dbContext.NotificationRecipients.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        entity.Name = request.Name.Trim();
        entity.Channel = request.Channel;
        entity.Destination = request.Destination.Trim();
        entity.IsActive = request.IsActive;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new NotificationRecipient
        {
            Id = entity.Id,
            Name = entity.Name,
            Channel = entity.Channel,
            Destination = entity.Destination,
            IsActive = entity.IsActive
        });
    }

    [HttpGet("notification-rules")]
    public async Task<ActionResult<IEnumerable<NotificationRule>>> GetNotificationRules(CancellationToken cancellationToken)
    {
        var rules = await dbContext.NotificationRules
            .AsNoTracking()
            .OrderBy(item => item.Name)
            .Select(item => new NotificationRule
            {
                Id = item.Id,
                Name = item.Name,
                EventType = item.EventType,
                Channel = item.Channel,
                RecipientId = item.RecipientId,
                IsEnabled = item.IsEnabled,
                PriorityFilter = item.PriorityFilter,
                BranchFilter = item.BranchFilter
            })
            .ToListAsync(cancellationToken);

        return Ok(rules);
    }

    [HttpPost("notification-rules")]
    public async Task<ActionResult<NotificationRule>> CreateNotificationRule([FromBody] UpsertNotificationRuleRequest request, CancellationToken cancellationToken)
    {
        var recipientExists = await dbContext.NotificationRecipients.AnyAsync(item => item.Id == request.RecipientId, cancellationToken);
        if (!recipientExists)
        {
            return ValidationProblem("RecipientId is invalid.");
        }

        var now = DateTimeOffset.UtcNow;
        var entity = new NotificationRuleEntity
        {
            Name = request.Name.Trim(),
            EventType = request.EventType,
            Channel = request.Channel,
            RecipientId = request.RecipientId,
            IsEnabled = request.IsEnabled,
            PriorityFilter = request.PriorityFilter,
            BranchFilter = request.BranchFilter,
            CreatedAt = now,
            UpdatedAt = now
        };

        dbContext.NotificationRules.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetNotificationRules), new { id = entity.Id }, new NotificationRule
        {
            Id = entity.Id,
            Name = entity.Name,
            EventType = entity.EventType,
            Channel = entity.Channel,
            RecipientId = entity.RecipientId,
            IsEnabled = entity.IsEnabled,
            PriorityFilter = entity.PriorityFilter,
            BranchFilter = entity.BranchFilter
        });
    }

    [HttpPut("notification-rules/{id:int}")]
    public async Task<ActionResult<NotificationRule>> UpdateNotificationRule(int id, [FromBody] UpsertNotificationRuleRequest request, CancellationToken cancellationToken)
    {
        var entity = await dbContext.NotificationRules.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        var recipientExists = await dbContext.NotificationRecipients.AnyAsync(item => item.Id == request.RecipientId, cancellationToken);
        if (!recipientExists)
        {
            return ValidationProblem("RecipientId is invalid.");
        }

        entity.Name = request.Name.Trim();
        entity.EventType = request.EventType;
        entity.Channel = request.Channel;
        entity.RecipientId = request.RecipientId;
        entity.IsEnabled = request.IsEnabled;
        entity.PriorityFilter = request.PriorityFilter;
        entity.BranchFilter = request.BranchFilter;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new NotificationRule
        {
            Id = entity.Id,
            Name = entity.Name,
            EventType = entity.EventType,
            Channel = entity.Channel,
            RecipientId = entity.RecipientId,
            IsEnabled = entity.IsEnabled,
            PriorityFilter = entity.PriorityFilter,
            BranchFilter = entity.BranchFilter
        });
    }

    [HttpGet("escalation-policies")]
    public async Task<ActionResult<IEnumerable<EscalationPolicy>>> GetEscalationPolicies(CancellationToken cancellationToken)
    {
        var policies = await dbContext.EscalationPolicies
            .AsNoTracking()
            .OrderBy(item => item.Name)
            .Select(item => new EscalationPolicy
            {
                Id = item.Id,
                Name = item.Name,
                PriorityFilter = item.PriorityFilter,
                BranchFilter = item.BranchFilter,
                WarningMinutesBeforeDue = item.WarningMinutesBeforeDue,
                EscalationDelayMinutes = item.EscalationDelayMinutes,
                MaxEscalationLevel = item.MaxEscalationLevel,
                ActiveFromHourUtc = item.ActiveFromHourUtc,
                ActiveToHourUtc = item.ActiveToHourUtc,
                IsEnabled = item.IsEnabled
            })
            .ToListAsync(cancellationToken);

        return Ok(policies);
    }

    [HttpPost("escalation-policies")]
    public async Task<ActionResult<EscalationPolicy>> CreateEscalationPolicy([FromBody] UpsertEscalationPolicyRequest request, CancellationToken cancellationToken)
    {
        if (request.WarningMinutesBeforeDue <= 0 || request.EscalationDelayMinutes <= 0 || request.MaxEscalationLevel <= 0)
        {
            return ValidationProblem("WarningMinutesBeforeDue, EscalationDelayMinutes, and MaxEscalationLevel must be greater than 0.");
        }

        var now = DateTimeOffset.UtcNow;
        var entity = new EscalationPolicyEntity
        {
            Name = request.Name.Trim(),
            PriorityFilter = request.PriorityFilter,
            BranchFilter = request.BranchFilter,
            WarningMinutesBeforeDue = request.WarningMinutesBeforeDue,
            EscalationDelayMinutes = request.EscalationDelayMinutes,
            MaxEscalationLevel = request.MaxEscalationLevel,
            ActiveFromHourUtc = request.ActiveFromHourUtc,
            ActiveToHourUtc = request.ActiveToHourUtc,
            IsEnabled = request.IsEnabled,
            CreatedAt = now,
            UpdatedAt = now
        };

        dbContext.EscalationPolicies.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetEscalationPolicies), new { id = entity.Id }, new EscalationPolicy
        {
            Id = entity.Id,
            Name = entity.Name,
            PriorityFilter = entity.PriorityFilter,
            BranchFilter = entity.BranchFilter,
            WarningMinutesBeforeDue = entity.WarningMinutesBeforeDue,
            EscalationDelayMinutes = entity.EscalationDelayMinutes,
            MaxEscalationLevel = entity.MaxEscalationLevel,
            ActiveFromHourUtc = entity.ActiveFromHourUtc,
            ActiveToHourUtc = entity.ActiveToHourUtc,
            IsEnabled = entity.IsEnabled
        });
    }

    [HttpPut("escalation-policies/{id:int}")]
    public async Task<ActionResult<EscalationPolicy>> UpdateEscalationPolicy(int id, [FromBody] UpsertEscalationPolicyRequest request, CancellationToken cancellationToken)
    {
        var entity = await dbContext.EscalationPolicies.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        entity.Name = request.Name.Trim();
        entity.PriorityFilter = request.PriorityFilter;
        entity.BranchFilter = request.BranchFilter;
        entity.WarningMinutesBeforeDue = request.WarningMinutesBeforeDue;
        entity.EscalationDelayMinutes = request.EscalationDelayMinutes;
        entity.MaxEscalationLevel = request.MaxEscalationLevel;
        entity.ActiveFromHourUtc = request.ActiveFromHourUtc;
        entity.ActiveToHourUtc = request.ActiveToHourUtc;
        entity.IsEnabled = request.IsEnabled;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new EscalationPolicy
        {
            Id = entity.Id,
            Name = entity.Name,
            PriorityFilter = entity.PriorityFilter,
            BranchFilter = entity.BranchFilter,
            WarningMinutesBeforeDue = entity.WarningMinutesBeforeDue,
            EscalationDelayMinutes = entity.EscalationDelayMinutes,
            MaxEscalationLevel = entity.MaxEscalationLevel,
            ActiveFromHourUtc = entity.ActiveFromHourUtc,
            ActiveToHourUtc = entity.ActiveToHourUtc,
            IsEnabled = entity.IsEnabled
        });
    }
}
