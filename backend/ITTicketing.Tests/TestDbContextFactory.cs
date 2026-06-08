using ITTicketing.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace ITTicketing.Tests;

internal static class TestDbContextFactory
{
    public static TicketDbContext Create(string? databaseName = null)
    {
        var options = new DbContextOptionsBuilder<TicketDbContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString())
            .Options;
        return new TicketDbContext(options);
    }
}
