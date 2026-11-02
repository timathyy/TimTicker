using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using TimTicker.Domain.Planning;
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

    public DbSet<DailyPlan> DailyPlans => Set<DailyPlan>();

    public DbSet<DailyPlanItem> DailyPlanItems => Set<DailyPlanItem>();

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

        modelBuilder.Entity<DailyPlan>(entity =>
        {
            entity.ToTable("DailyPlans");
            entity.HasKey(plan => plan.Id);
            entity.HasIndex(plan => plan.Date).IsUnique();
            entity.Property(plan => plan.CreatedAtUtc).HasConversion(UtcTicksConverter);
            entity.Property(plan => plan.UpdatedAtUtc).HasConversion(UtcTicksConverter);
            entity.Property(plan => plan.ClosedAtUtc).HasConversion(NullableUtcTicksConverter);
            entity.Property(plan => plan.ReviewText).HasMaxLength(4_000);
            entity.Ignore(plan => plan.ActiveItems);
            entity.Navigation(plan => plan.Items).UsePropertyAccessMode(PropertyAccessMode.Field);
            entity.HasMany(plan => plan.Items)
                .WithOne()
                .HasForeignKey(item => item.DailyPlanId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DailyPlanItem>(entity =>
        {
            entity.ToTable("DailyPlanItems");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.DailyPlanId, item.TaskItemId }).IsUnique();
            entity.HasIndex(item => new { item.DailyPlanId, item.RemovedAtUtc, item.SortOrder });
            entity.Property(item => item.AddedAtUtc).HasConversion(UtcTicksConverter);
            entity.Property(item => item.RemovedAtUtc).HasConversion(NullableUtcTicksConverter);
            entity.Ignore(item => item.IsActive);
            entity.HasOne<TaskItem>()
                .WithMany()
                .HasForeignKey(item => item.TaskItemId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
