using ITTicketing.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ITTicketing.Api.Data;

public sealed class TicketDbContext(DbContextOptions<TicketDbContext> options) : DbContext(options)
{
    public DbSet<TicketEntity> Tickets => Set<TicketEntity>();
    public DbSet<TicketAuditEntryEntity> TicketAuditEntries => Set<TicketAuditEntryEntity>();
    public DbSet<NotificationRecipientEntity> NotificationRecipients => Set<NotificationRecipientEntity>();
    public DbSet<NotificationRuleEntity> NotificationRules => Set<NotificationRuleEntity>();
    public DbSet<EscalationPolicyEntity> EscalationPolicies => Set<EscalationPolicyEntity>();
    public DbSet<TicketOperationalEventEntity> TicketOperationalEvents => Set<TicketOperationalEventEntity>();
    public DbSet<NotificationDeliveryAttemptEntity> NotificationDeliveryAttempts => Set<NotificationDeliveryAttemptEntity>();
    public DbSet<SlaSignalEntity> SlaSignals => Set<SlaSignalEntity>();
    public DbSet<EscalationActionEntity> EscalationActions => Set<EscalationActionEntity>();
    public DbSet<ComplianceRecordEntity> ComplianceRecords => Set<ComplianceRecordEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var ticketBuilder = modelBuilder.Entity<TicketEntity>();
        ticketBuilder.ToTable("Tickets");
        ticketBuilder.HasKey(ticket => ticket.Id);
        ticketBuilder.Property(ticket => ticket.Title).IsRequired().HasMaxLength(200);
        ticketBuilder.Property(ticket => ticket.Description).IsRequired().HasMaxLength(4000);
        ticketBuilder.Property(ticket => ticket.SubmittedBy).IsRequired().HasMaxLength(200);
        ticketBuilder.Property(ticket => ticket.AssignedTo).HasMaxLength(200);
        ticketBuilder.Property(ticket => ticket.Branch).HasConversion<string>().HasMaxLength(50);
        ticketBuilder.Property(ticket => ticket.Priority).HasConversion<string>().HasMaxLength(50);
        ticketBuilder.Property(ticket => ticket.Status).HasConversion<string>().HasMaxLength(50);
        ticketBuilder.HasMany(ticket => ticket.AuditLog)
            .WithOne(entry => entry.Ticket)
            .HasForeignKey(entry => entry.TicketId)
            .OnDelete(DeleteBehavior.Cascade);
        ticketBuilder.HasMany(ticket => ticket.OperationalEvents)
            .WithOne(entry => entry.Ticket)
            .HasForeignKey(entry => entry.TicketId)
            .OnDelete(DeleteBehavior.Cascade);
        ticketBuilder.HasMany(ticket => ticket.SlaSignals)
            .WithOne(signal => signal.Ticket)
            .HasForeignKey(signal => signal.TicketId)
            .OnDelete(DeleteBehavior.Cascade);
        ticketBuilder.HasMany(ticket => ticket.EscalationActions)
            .WithOne(action => action.Ticket)
            .HasForeignKey(action => action.TicketId)
            .OnDelete(DeleteBehavior.Cascade);
        ticketBuilder.HasMany(ticket => ticket.ComplianceRecords)
            .WithOne(record => record.Ticket)
            .HasForeignKey(record => record.TicketId)
            .OnDelete(DeleteBehavior.Cascade);
        ticketBuilder.HasIndex(ticket => ticket.CreatedAt);
        ticketBuilder.HasIndex(ticket => ticket.DueBy);
        ticketBuilder.HasIndex(ticket => ticket.Status);

        var auditBuilder = modelBuilder.Entity<TicketAuditEntryEntity>();
        auditBuilder.ToTable("TicketAuditEntries");
        auditBuilder.HasKey(entry => entry.Id);
        auditBuilder.Property(entry => entry.Action).IsRequired().HasMaxLength(200);
        auditBuilder.Property(entry => entry.PerformedBy).IsRequired().HasMaxLength(200);

        var recipientBuilder = modelBuilder.Entity<NotificationRecipientEntity>();
        recipientBuilder.ToTable("NotificationRecipients");
        recipientBuilder.HasKey(item => item.Id);
        recipientBuilder.Property(item => item.Name).IsRequired().HasMaxLength(200);
        recipientBuilder.Property(item => item.Channel).HasConversion<string>().HasMaxLength(30);
        recipientBuilder.Property(item => item.Destination).IsRequired().HasMaxLength(500);

        var ruleBuilder = modelBuilder.Entity<NotificationRuleEntity>();
        ruleBuilder.ToTable("NotificationRules");
        ruleBuilder.HasKey(item => item.Id);
        ruleBuilder.Property(item => item.Name).IsRequired().HasMaxLength(200);
        ruleBuilder.Property(item => item.EventType).HasConversion<string>().HasMaxLength(50);
        ruleBuilder.Property(item => item.Channel).HasConversion<string>().HasMaxLength(30);
        ruleBuilder.Property(item => item.PriorityFilter).HasConversion<string>().HasMaxLength(50);
        ruleBuilder.Property(item => item.BranchFilter).HasConversion<string>().HasMaxLength(50);
        ruleBuilder.HasOne(item => item.Recipient)
            .WithMany(item => item.NotificationRules)
            .HasForeignKey(item => item.RecipientId)
            .OnDelete(DeleteBehavior.Cascade);

        var escalationPolicyBuilder = modelBuilder.Entity<EscalationPolicyEntity>();
        escalationPolicyBuilder.ToTable("EscalationPolicies");
        escalationPolicyBuilder.HasKey(item => item.Id);
        escalationPolicyBuilder.Property(item => item.Name).IsRequired().HasMaxLength(200);
        escalationPolicyBuilder.Property(item => item.PriorityFilter).HasConversion<string>().HasMaxLength(50);
        escalationPolicyBuilder.Property(item => item.BranchFilter).HasConversion<string>().HasMaxLength(50);

        var eventBuilder = modelBuilder.Entity<TicketOperationalEventEntity>();
        eventBuilder.ToTable("TicketOperationalEvents");
        eventBuilder.HasKey(item => item.Id);
        eventBuilder.Property(item => item.EventType).HasConversion<string>().HasMaxLength(50);
        eventBuilder.Property(item => item.TriggeredBy).IsRequired().HasMaxLength(200);
        eventBuilder.Property(item => item.DetailsJson).IsRequired();
        eventBuilder.HasIndex(item => new { item.TicketId, item.EventType, item.OccurredAt });

        var attemptBuilder = modelBuilder.Entity<NotificationDeliveryAttemptEntity>();
        attemptBuilder.ToTable("NotificationDeliveryAttempts");
        attemptBuilder.HasKey(item => item.Id);
        attemptBuilder.Property(item => item.Channel).HasConversion<string>().HasMaxLength(30);
        attemptBuilder.Property(item => item.FailureReason).HasMaxLength(1000);
        attemptBuilder.HasOne(item => item.TicketOperationalEvent)
            .WithMany(item => item.DeliveryAttempts)
            .HasForeignKey(item => item.TicketOperationalEventId)
            .OnDelete(DeleteBehavior.Cascade);
        attemptBuilder.HasOne(item => item.Recipient)
            .WithMany(item => item.DeliveryAttempts)
            .HasForeignKey(item => item.RecipientId)
            .OnDelete(DeleteBehavior.Restrict);

        var signalBuilder = modelBuilder.Entity<SlaSignalEntity>();
        signalBuilder.ToTable("SlaSignals");
        signalBuilder.HasKey(item => item.Id);
        signalBuilder.Property(item => item.SignalType).HasConversion<string>().HasMaxLength(30);
        signalBuilder.HasIndex(item => new { item.TicketId, item.SignalType, item.ThresholdMinutes }).IsUnique();

        var escalationActionBuilder = modelBuilder.Entity<EscalationActionEntity>();
        escalationActionBuilder.ToTable("EscalationActions");
        escalationActionBuilder.HasKey(item => item.Id);
        escalationActionBuilder.Property(item => item.Reason).IsRequired().HasMaxLength(500);
        escalationActionBuilder.HasOne(item => item.EscalationPolicy)
            .WithMany()
            .HasForeignKey(item => item.EscalationPolicyId)
            .OnDelete(DeleteBehavior.Restrict);
        escalationActionBuilder.HasIndex(item => new { item.TicketId, item.EscalationPolicyId, item.EscalationLevel }).IsUnique();

        var complianceBuilder = modelBuilder.Entity<ComplianceRecordEntity>();
        complianceBuilder.ToTable("ComplianceRecords");
        complianceBuilder.HasKey(item => item.Id);
        complianceBuilder.Property(item => item.RecordType).IsRequired().HasMaxLength(100);
        complianceBuilder.Property(item => item.PayloadJson).IsRequired();
        complianceBuilder.HasIndex(item => new { item.TicketId, item.CreatedAt });
    }
}
