using ITTicketing.Api.Data.Entities;
using ITTicketing.Api.Models;
using ITTicketing.Api.Services;
using NSubstitute;

namespace ITTicketing.Tests;

public sealed class SlaEvaluatorTests
{
    private static TicketEntity MakeTicket(DateTimeOffset dueBy, TicketStatus status = TicketStatus.New)
    {
        return new TicketEntity
        {
            Id = Guid.NewGuid(),
            Title = "Test Ticket",
            Description = "desc",
            Branch = BranchLocation.HeadOffice,
            Priority = TicketPriority.Medium,
            Status = status,
            SubmittedBy = "tester",
            DueBy = dueBy
        };
    }

    [Fact]
    public async Task EvaluateAsync_TicketPastDue_RaisesBreachSignal()
    {
        using var dbContext = TestDbContextFactory.Create();
        var ticket = MakeTicket(DateTimeOffset.UtcNow.AddHours(-1));
        dbContext.Tickets.Add(ticket);
        await dbContext.SaveChangesAsync();

        var sender = Substitute.For<INotificationChannelSender>();
        sender.SendAsync(Arg.Any<NotificationMessage>(), Arg.Any<CancellationToken>())
            .Returns(new NotificationSendResult(true));
        var orchestrator = new TicketEventOrchestrator(dbContext, sender);
        var engine = new EscalationEngine(dbContext, orchestrator);
        var evaluator = new SlaEvaluator(dbContext, orchestrator, engine);

        await evaluator.EvaluateAsync();

        var signals = dbContext.SlaSignals.ToList();
        Assert.Contains(signals, s => s.SignalType == SlaSignalType.Breach && s.TicketId == ticket.Id);
    }

    [Fact]
    public async Task EvaluateAsync_TicketNotYetDue_DoesNotRaiseBreachSignal()
    {
        using var dbContext = TestDbContextFactory.Create();
        var ticket = MakeTicket(DateTimeOffset.UtcNow.AddHours(2));
        dbContext.Tickets.Add(ticket);
        await dbContext.SaveChangesAsync();

        var sender = Substitute.For<INotificationChannelSender>();
        var orchestrator = new TicketEventOrchestrator(dbContext, sender);
        var engine = new EscalationEngine(dbContext, orchestrator);
        var evaluator = new SlaEvaluator(dbContext, orchestrator, engine);

        await evaluator.EvaluateAsync();

        Assert.Empty(dbContext.SlaSignals.Where(s => s.SignalType == SlaSignalType.Breach));
    }

    [Fact]
    public async Task EvaluateAsync_BreachAlreadyRaised_DoesNotDuplicate()
    {
        using var dbContext = TestDbContextFactory.Create();
        var ticket = MakeTicket(DateTimeOffset.UtcNow.AddHours(-1));
        dbContext.Tickets.Add(ticket);
        dbContext.SlaSignals.Add(new SlaSignalEntity
        {
            TicketId = ticket.Id,
            SignalType = SlaSignalType.Breach,
            TriggeredAt = DateTimeOffset.UtcNow.AddMinutes(-30)
        });
        await dbContext.SaveChangesAsync();

        var sender = Substitute.For<INotificationChannelSender>();
        var orchestrator = new TicketEventOrchestrator(dbContext, sender);
        var engine = new EscalationEngine(dbContext, orchestrator);
        var evaluator = new SlaEvaluator(dbContext, orchestrator, engine);

        await evaluator.EvaluateAsync();

        Assert.Single(dbContext.SlaSignals.Where(s => s.SignalType == SlaSignalType.Breach));
    }

    [Fact]
    public async Task EvaluateAsync_TicketInWarningWindow_RaisesWarningSignal()
    {
        using var dbContext = TestDbContextFactory.Create();
        // Due in 45 minutes — within the default 60-minute warning threshold
        var ticket = MakeTicket(DateTimeOffset.UtcNow.AddMinutes(45));
        dbContext.Tickets.Add(ticket);
        await dbContext.SaveChangesAsync();

        var sender = Substitute.For<INotificationChannelSender>();
        sender.SendAsync(Arg.Any<NotificationMessage>(), Arg.Any<CancellationToken>())
            .Returns(new NotificationSendResult(true));
        var orchestrator = new TicketEventOrchestrator(dbContext, sender);
        var engine = new EscalationEngine(dbContext, orchestrator);
        var evaluator = new SlaEvaluator(dbContext, orchestrator, engine);

        await evaluator.EvaluateAsync();

        var signals = dbContext.SlaSignals.ToList();
        Assert.Contains(signals, s => s.SignalType == SlaSignalType.Warning && s.TicketId == ticket.Id);
    }

    [Fact]
    public async Task EvaluateAsync_WarningAlreadyRaised_DoesNotDuplicate()
    {
        using var dbContext = TestDbContextFactory.Create();
        var ticket = MakeTicket(DateTimeOffset.UtcNow.AddMinutes(45));
        dbContext.Tickets.Add(ticket);
        dbContext.SlaSignals.Add(new SlaSignalEntity
        {
            TicketId = ticket.Id,
            SignalType = SlaSignalType.Warning,
            ThresholdMinutes = 60,
            TriggeredAt = DateTimeOffset.UtcNow.AddMinutes(-10)
        });
        await dbContext.SaveChangesAsync();

        var sender = Substitute.For<INotificationChannelSender>();
        var orchestrator = new TicketEventOrchestrator(dbContext, sender);
        var engine = new EscalationEngine(dbContext, orchestrator);
        var evaluator = new SlaEvaluator(dbContext, orchestrator, engine);

        await evaluator.EvaluateAsync();

        Assert.Single(dbContext.SlaSignals.Where(s => s.SignalType == SlaSignalType.Warning && s.ThresholdMinutes == 60));
    }

    [Fact]
    public async Task EvaluateAsync_TicketDueFarInFuture_NoSignalsRaised()
    {
        using var dbContext = TestDbContextFactory.Create();
        var ticket = MakeTicket(DateTimeOffset.UtcNow.AddHours(5));
        dbContext.Tickets.Add(ticket);
        await dbContext.SaveChangesAsync();

        var sender = Substitute.For<INotificationChannelSender>();
        var orchestrator = new TicketEventOrchestrator(dbContext, sender);
        var engine = new EscalationEngine(dbContext, orchestrator);
        var evaluator = new SlaEvaluator(dbContext, orchestrator, engine);

        await evaluator.EvaluateAsync();

        Assert.Empty(dbContext.SlaSignals);
    }

    [Fact]
    public async Task EvaluateAsync_ResolvedTicket_NoSignalsRaised()
    {
        using var dbContext = TestDbContextFactory.Create();
        var ticket = MakeTicket(DateTimeOffset.UtcNow.AddHours(-1), TicketStatus.Resolved);
        dbContext.Tickets.Add(ticket);
        await dbContext.SaveChangesAsync();

        var sender = Substitute.For<INotificationChannelSender>();
        var orchestrator = new TicketEventOrchestrator(dbContext, sender);
        var engine = new EscalationEngine(dbContext, orchestrator);
        var evaluator = new SlaEvaluator(dbContext, orchestrator, engine);

        await evaluator.EvaluateAsync();

        Assert.Empty(dbContext.SlaSignals);
    }

    [Fact]
    public async Task EvaluateAsync_BreachTriggered_AlsoEvaluatesEscalations()
    {
        using var dbContext = TestDbContextFactory.Create();
        var ticket = MakeTicket(DateTimeOffset.UtcNow.AddHours(-1));
        dbContext.Tickets.Add(ticket);
        dbContext.EscalationPolicies.Add(new EscalationPolicyEntity
        {
            Id = 1,
            Name = "Auto Escalate",
            MaxEscalationLevel = 3,
            EscalationDelayMinutes = 0,
            WarningMinutesBeforeDue = 60,
            IsEnabled = true
        });
        await dbContext.SaveChangesAsync();

        var sender = Substitute.For<INotificationChannelSender>();
        sender.SendAsync(Arg.Any<NotificationMessage>(), Arg.Any<CancellationToken>())
            .Returns(new NotificationSendResult(true));
        var orchestrator = new TicketEventOrchestrator(dbContext, sender);
        var engine = new EscalationEngine(dbContext, orchestrator);
        var evaluator = new SlaEvaluator(dbContext, orchestrator, engine);

        await evaluator.EvaluateAsync();

        Assert.NotEmpty(dbContext.EscalationActions);
    }
}
