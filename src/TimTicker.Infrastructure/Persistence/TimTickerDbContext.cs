using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using TimTicker.Domain.Tasks;

namespace TimTicker.Infrastructure.Persistence;

public sealed class TimTickerDbContext(DbContextOptions<TimTickerDbContext> options) : DbContext(options)
{
    private static readonly ValueConverter<DateTimeOffset, long> UtcTicksConverter = new(
        value => value.UtcTicks,
        value => new DateTimeOffset(value, TimeSpan.Zero));

    private static readonly ValueConverter<DateTimeOffset?, long?> NullableUtcTicksConverter = new(
        value => value.HasValue ? value.Value.UtcTicks : null,
        value => value.HasValue ? new DateTimeOffset(value.Value, TimeSpan.Zero) : null);

    public DbSet<TaskItem> Tasks => Set<TaskItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TaskItem>(entity =>
        {
            entity.ToTable("Tasks");
            entity.HasKey(task => task.Id);
            entity.Property(task => task.Title).HasMaxLength(TaskItem.MaximumTitleLength).IsRequired();
            entity.Property(task => task.Description).HasMaxLength(TaskItem.MaximumDescriptionLength);
            entity.Property(task => task.Priority).HasConversion<int>();
            entity.Property(task => task.Status).HasConversion<int>();
            entity.Property(task => task.CreatedAtUtc).HasConversion(UtcTicksConverter);
            entity.Property(task => task.UpdatedAtUtc).HasConversion(UtcTicksConverter);
            entity.Property(task => task.CompletedAtUtc).HasConversion(NullableUtcTicksConverter);
            entity.HasIndex(task => new { task.IsDeleted, task.Status, task.CreatedAtUtc });
            entity.HasIndex(task => new { task.IsDeleted, task.DueDate });
        });
    }
}
