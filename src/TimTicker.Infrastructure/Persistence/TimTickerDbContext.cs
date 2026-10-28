using Microsoft.EntityFrameworkCore;

namespace TimTicker.Infrastructure.Persistence;

public sealed class TimTickerDbContext(DbContextOptions<TimTickerDbContext> options) : DbContext(options);
