using ITTicketing.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ITTicketing.Api.Data;

public sealed class TicketDbContext(DbContextOptions<TicketDbContext> options) : DbContext(options)
{
    public DbSet<TicketEntity> Tickets => Set<TicketEntity>();
    public DbSet<TicketAuditEntryEntity> TicketAuditEntries => Set<TicketAuditEntryEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var ticketBuilder = modelBuilder.Entity<TicketEntity>();
        ticketBuilder.ToTable("Tickets");
        ticketBuilder.HasKey(ticket => ticket.Id);
        ticketBuilder.Property(ticket => ticket.Title).IsRequired().HasMaxLength(200);
        ticketBuilder.Property(ticket => ticket.Description).IsRequired().HasMaxLength(4000);
        ticketBuilder.Property(ticket => ticket.SubmittedBy).IsRequired().HasMaxLength(200);
        ticketBuilder.Property(ticket => ticket.Branch).HasConversion<string>().HasMaxLength(50);
        ticketBuilder.Property(ticket => ticket.Priority).HasConversion<string>().HasMaxLength(50);
        ticketBuilder.Property(ticket => ticket.Status).HasConversion<string>().HasMaxLength(50);
        ticketBuilder.HasMany(ticket => ticket.AuditLog)
            .WithOne(entry => entry.Ticket)
            .HasForeignKey(entry => entry.TicketId)
            .OnDelete(DeleteBehavior.Cascade);
        ticketBuilder.HasIndex(ticket => ticket.CreatedAt);

        var auditBuilder = modelBuilder.Entity<TicketAuditEntryEntity>();
        auditBuilder.ToTable("TicketAuditEntries");
        auditBuilder.HasKey(entry => entry.Id);
        auditBuilder.Property(entry => entry.Action).IsRequired().HasMaxLength(200);
        auditBuilder.Property(entry => entry.PerformedBy).IsRequired().HasMaxLength(200);
    }
}
