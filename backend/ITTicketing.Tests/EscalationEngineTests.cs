using ITTicketing.Api.Data.Entities;
using ITTicketing.Api.Models;
using ITTicketing.Api.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace ITTicketing.Tests;

public sealed class EscalationEngineTests
{
    private static TicketEntity MakeOverdueTicket(TicketPriority priority = TicketPriority.High, BranchLocation branch = BranchLocation.HeadOffice)
    {
        return new TicketEntity
        {
            Id = Guid.NewGuid(),
            Title = "Test Ticket",
            Description = "desc",
            Branch = branch,
            Priority = priority,
            Status = TicketStatus.New,
            SubmittedBy = "tester",
            DueBy = DateTimeOffset.UtcNow.AddHours(-1)
        };
    }

    private static EscalationPolicyEntity MakePolicy(
        int maxLevel = 3,
        int delayMinutes = 0,
        TicketPriority? priorityFilter = null,
        BranchLocation? branchFilter = null,
        int? activeFromHour = null,
        int? activeToHour = null)
    {
        return new EscalationPolicyEntity
        {
            Id = 1,
            Name = "Test Policy",
            MaxEscalationLevel = maxLevel,
            EscalationDelayMinutes = delayMinutes,
            WarningMinutesBeforeDue = 60,
            PriorityFilter = priorityFilter,
            BranchFilter = branchFilter,
            ActiveFromHourUtc = activeFromHour,
            ActiveToHourUtc = activeToHour,
            IsEnabled = true
        };
    }

    [Fact]
    public async Task EvaluateEscalationsAsync_OverdueTicketWithApplicablePolicy_AddsFirstEscalationAction()
    {
        using var dbContext = TestDbContextFactory.Create();
        var ticket = MakeOverdueTicket();
        dbContext.Tickets.Add(ticket);
        var policy = MakePolicy();
        dbContext.EscalationPolicies.Add(policy);
        await dbContext.SaveChangesAsync();

        var sender = Substitute.For<INotificationChannelSender>();
        var orchestrator = new TicketEventOrchestrator(dbContext, sender);
        var engine = new EscalationEngine(dbContext, orchestrator);

        await engine.EvaluateEscalationsAsync([ticket]);

        var actions = dbContext.EscalationActions.ToList();
        Assert.Single(actions);
        Assert.Equal(1, actions[0].EscalationLevel);
        Assert.Equal(ticket.Id, actions[0].TicketId);
        Assert.Equal(policy.Id, actions[0].EscalationPolicyId);
    }

    [Fact]
    public async Task EvaluateEscalationsAsync_TicketAlreadyAtMaxLevel_DoesNotAddFurtherAction()
    {
        using var dbContext = TestDbContextFactory.Create();
        var ticket = MakeOverdueTicket();
        dbContext.Tickets.Add(ticket);
        var policy = MakePolicy(maxLevel: 2);
        dbContext.EscalationPolicies.Add(policy);
        await dbContext.SaveChangesAsync();

        dbContext.EscalationActions.Add(new EscalationActionEntity
        {
            TicketId = ticket.Id,
            EscalationPolicyId = policy.Id,
            EscalationLevel = 2,
            Reason = "Level 2",
            TriggeredAt = DateTimeOffset.UtcNow.AddHours(-1)
        });
        await dbContext.SaveChangesAsync();

        var sender = Substitute.For<INotificationChannelSender>();
        var orchestrator = new TicketEventOrchestrator(dbContext, sender);
        var engine = new EscalationEngine(dbContext, orchestrator);

        await engine.EvaluateEscalationsAsync([ticket]);

        Assert.Equal(1, dbContext.EscalationActions.Count());
    }

    [Fact]
    public async Task EvaluateEscalationsAsync_EscalationDelayNotElapsed_DoesNotEscalate()
    {
        using var dbContext = TestDbContextFactory.Create();
        var ticket = MakeOverdueTicket();
        dbContext.Tickets.Add(ticket);
        var policy = MakePolicy(delayMinutes: 120);
        dbContext.EscalationPolicies.Add(policy);
        await dbContext.SaveChangesAsync();

        dbContext.EscalationActions.Add(new EscalationActionEntity
        {
            TicketId = ticket.Id,
            EscalationPolicyId = policy.Id,
            EscalationLevel = 1,
            Reason = "Level 1",
            TriggeredAt = DateTimeOffset.UtcNow.AddMinutes(-30)
        });
        await dbContext.SaveChangesAsync();

        var sender = Substitute.For<INotificationChannelSender>();
        var orchestrator = new TicketEventOrchestrator(dbContext, sender);
        var engine = new EscalationEngine(dbContext, orchestrator);

        await engine.EvaluateEscalationsAsync([ticket]);

        Assert.Equal(1, dbContext.EscalationActions.Count());
    }

    [Fact]
    public async Task EvaluateEscalationsAsync_EscalationDelayElapsed_EscalatesToNextLevel()
    {
        using var dbContext = TestDbContextFactory.Create();
        var ticket = MakeOverdueTicket();
        dbContext.Tickets.Add(ticket);
        var policy = MakePolicy(maxLevel: 3, delayMinutes: 30);
        dbContext.EscalationPolicies.Add(policy);
        await dbContext.SaveChangesAsync();

        dbContext.EscalationActions.Add(new EscalationActionEntity
        {
            TicketId = ticket.Id,
            EscalationPolicyId = policy.Id,
            EscalationLevel = 1,
            Reason = "Level 1",
            TriggeredAt = DateTimeOffset.UtcNow.AddMinutes(-60)
        });
        await dbContext.SaveChangesAsync();

        var sender = Substitute.For<INotificationChannelSender>();
        var orchestrator = new TicketEventOrchestrator(dbContext, sender);
        var engine = new EscalationEngine(dbContext, orchestrator);

        await engine.EvaluateEscalationsAsync([ticket]);

        var actions = dbContext.EscalationActions.OrderBy(a => a.EscalationLevel).ToList();
        Assert.Equal(2, actions.Count);
        Assert.Equal(2, actions[1].EscalationLevel);
    }

    [Fact]
    public async Task EvaluateEscalationsAsync_PriorityFilterMismatch_DoesNotEscalate()
    {
        using var dbContext = TestDbContextFactory.Create();
        var ticket = MakeOverdueTicket(priority: TicketPriority.Low);
        dbContext.Tickets.Add(ticket);
        var policy = MakePolicy(priorityFilter: TicketPriority.Critical);
        dbContext.EscalationPolicies.Add(policy);
        await dbContext.SaveChangesAsync();

        var sender = Substitute.For<INotificationChannelSender>();
        var orchestrator = new TicketEventOrchestrator(dbContext, sender);
        var engine = new EscalationEngine(dbContext, orchestrator);

        await engine.EvaluateEscalationsAsync([ticket]);

        Assert.Empty(dbContext.EscalationActions);
    }

    [Fact]
    public async Task EvaluateEscalationsAsync_BranchFilterMismatch_DoesNotEscalate()
    {
        using var dbContext = TestDbContextFactory.Create();
        var ticket = MakeOverdueTicket(branch: BranchLocation.Branch1);
        dbContext.Tickets.Add(ticket);
        var policy = MakePolicy(branchFilter: BranchLocation.Branch2);
        dbContext.EscalationPolicies.Add(policy);
        await dbContext.SaveChangesAsync();

        var sender = Substitute.For<INotificationChannelSender>();
        var orchestrator = new TicketEventOrchestrator(dbContext, sender);
        var engine = new EscalationEngine(dbContext, orchestrator);

        await engine.EvaluateEscalationsAsync([ticket]);

        Assert.Empty(dbContext.EscalationActions);
    }

    [Fact]
    public async Task EvaluateEscalationsAsync_PolicyMatchingPriorityAndBranch_Escalates()
    {
        using var dbContext = TestDbContextFactory.Create();
        var ticket = MakeOverdueTicket(priority: TicketPriority.Critical, branch: BranchLocation.Branch3);
        dbContext.Tickets.Add(ticket);
        var policy = MakePolicy(priorityFilter: TicketPriority.Critical, branchFilter: BranchLocation.Branch3);
        dbContext.EscalationPolicies.Add(policy);
        await dbContext.SaveChangesAsync();

        var sender = Substitute.For<INotificationChannelSender>();
        var orchestrator = new TicketEventOrchestrator(dbContext, sender);
        var engine = new EscalationEngine(dbContext, orchestrator);

        await engine.EvaluateEscalationsAsync([ticket]);

        Assert.Single(dbContext.EscalationActions);
    }

    [Fact]
    public async Task EvaluateEscalationsAsync_DisabledPolicy_DoesNotEscalate()
    {
        using var dbContext = TestDbContextFactory.Create();
        var ticket = MakeOverdueTicket();
        dbContext.Tickets.Add(ticket);
        dbContext.EscalationPolicies.Add(new EscalationPolicyEntity
        {
            Id = 1,
            Name = "Disabled",
            MaxEscalationLevel = 3,
            EscalationDelayMinutes = 0,
            WarningMinutesBeforeDue = 60,
            IsEnabled = false
        });
        await dbContext.SaveChangesAsync();

        var sender = Substitute.For<INotificationChannelSender>();
        var orchestrator = new TicketEventOrchestrator(dbContext, sender);
        var engine = new EscalationEngine(dbContext, orchestrator);

        await engine.EvaluateEscalationsAsync([ticket]);

        Assert.Empty(dbContext.EscalationActions);
    }

    [Fact]
    public async Task GetWarningThresholdsForTicketAsync_NoPolicies_ReturnDefaultThresholds()
    {
        using var dbContext = TestDbContextFactory.Create();
        var ticket = MakeOverdueTicket();

        var sender = Substitute.For<INotificationChannelSender>();
        var orchestrator = new TicketEventOrchestrator(dbContext, sender);
        var engine = new EscalationEngine(dbContext, orchestrator);

        var thresholds = await engine.GetWarningThresholdsForTicketAsync(ticket);

        Assert.Contains(60, thresholds);
        Assert.Contains(30, thresholds);
    }

    [Fact]
    public async Task GetWarningThresholdsForTicketAsync_PolicyThresholdMergedWithDefaults()
    {
        using var dbContext = TestDbContextFactory.Create();
        var ticket = MakeOverdueTicket();
        dbContext.EscalationPolicies.Add(MakePolicy());
        await dbContext.SaveChangesAsync();

        var sender = Substitute.For<INotificationChannelSender>();
        var orchestrator = new TicketEventOrchestrator(dbContext, sender);
        var engine = new EscalationEngine(dbContext, orchestrator);

        var thresholds = await engine.GetWarningThresholdsForTicketAsync(ticket);

        Assert.Contains(60, thresholds);
        Assert.Equal(thresholds, thresholds.Distinct().ToList());
    }
}
