using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TimTicker.Infrastructure.Persistence;

public sealed class TimTickerDbContextFactory : IDesignTimeDbContextFactory<TimTickerDbContext>
{
    public TimTickerDbContext CreateDbContext(string[] args)
    {
        DbContextOptions<TimTickerDbContext> options = new DbContextOptionsBuilder<TimTickerDbContext>()
            .UseSqlite("Data Source=timticker.design.db")
            .Options;

        return new TimTickerDbContext(options);
    }
}
