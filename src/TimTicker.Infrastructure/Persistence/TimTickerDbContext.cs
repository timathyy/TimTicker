using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using TimTicker.Domain.Planning;
using TimTicker.Domain.Tasks;
using TimTicker.Domain.Timeline;
using TimTicker.Domain.Templates;

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

    public DbSet<TaskFolder> TaskFolders => Set<TaskFolder>();

    public DbSet<TaskTemplate> TaskTemplates => Set<TaskTemplate>();

    public DbSet<DailyPlan> DailyPlans => Set<DailyPlan>();

    public DbSet<DailyPlanItem> DailyPlanItems => Set<DailyPlanItem>();

    public DbSet<TimelineEntry> TimelineEntries => Set<TimelineEntry>();

    public DbSet<TimelineEntryTaskLink> TimelineEntryTaskLinks => Set<TimelineEntryTaskLink>();

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
            entity.HasIndex(task => new { task.FolderId, task.IsDeleted, task.CreatedAtUtc });
            entity.HasIndex(task => new { task.TemplateId, task.TemplateSequenceNumber }).IsUnique();
            entity.HasIndex(task => new { task.TemplateId, task.TemplateOccurrenceDate }).IsUnique();
            entity.HasOne<TaskFolder>()
                .WithMany()
                .HasForeignKey(task => task.FolderId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne<TaskTemplate>()
                .WithMany()
                .HasForeignKey(task => task.TemplateId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<TaskFolder>(entity =>
        {
            entity.ToTable("TaskFolders");
            entity.HasKey(folder => folder.Id);
            entity.Property(folder => folder.Name)
                .UseCollation("NOCASE")
                .HasMaxLength(TaskFolder.MaximumNameLength)
                .IsRequired();
            entity.Property(folder => folder.Kind).HasConversion<int>();
            entity.Property(folder => folder.CreatedAtUtc).HasConversion(UtcTicksConverter);
            entity.Property(folder => folder.UpdatedAtUtc).HasConversion(UtcTicksConverter);
            entity.HasIndex(folder => folder.Name)
                .IsUnique()
                .HasFilter("ParentId IS NULL AND IsDeleted = 0");
            entity.HasIndex(folder => new { folder.ParentId, folder.Name })
                .IsUnique()
                .HasFilter("ParentId IS NOT NULL AND IsDeleted = 0");
            entity.HasIndex(folder => new { folder.IsDeleted, folder.Kind, folder.Name });
            entity.HasOne<TaskFolder>()
                .WithMany()
                .HasForeignKey(folder => folder.ParentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<TaskTemplate>(entity =>
        {
            entity.ToTable("TaskTemplates");
            entity.HasKey(template => template.Id);
            entity.Property(template => template.Name)
                .UseCollation("NOCASE")
                .HasMaxLength(TaskTemplate.MaximumNameLength)
                .IsRequired();
            entity.Property(template => template.TitlePattern)
                .HasMaxLength(TaskTemplate.MaximumTitlePatternLength)
                .IsRequired();
            entity.Property(template => template.Description).HasMaxLength(TaskItem.MaximumDescriptionLength);
            entity.Property(template => template.Priority).HasConversion<int>();
            entity.Property(template => template.Schedule).HasConversion<int>();
            entity.Property(template => template.CreatedAtUtc).HasConversion(UtcTicksConverter);
            entity.Property(template => template.UpdatedAtUtc).HasConversion(UtcTicksConverter);
            entity.HasIndex(template => template.Name);
            entity.HasIndex(template => template.FolderId).IsUnique();
            entity.HasOne<TaskFolder>()
                .WithMany()
                .HasForeignKey(template => template.FolderId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<DailyPlan>(entity =>
        {
            entity.ToTable("DailyPlans");
            entity.HasKey(plan => plan.Id);
            entity.HasIndex(plan => plan.Date).IsUnique();
            entity.Property(plan => plan.CreatedAtUtc).HasConversion(UtcTicksConverter);
            entity.Property(plan => plan.UpdatedAtUtc).HasConversion(UtcTicksConverter);
            entity.Property(plan => plan.ClosedAtUtc).HasConversion(NullableUtcTicksConverter);
            entity.Property(plan => plan.ReviewText).HasMaxLength(DailyPlan.MaximumReviewLength);
            entity.Ignore(plan => plan.ActiveItems);
            entity.Ignore(plan => plan.IsClosed);
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

        modelBuilder.Entity<TimelineEntry>(entity =>
        {
            entity.ToTable("TimelineEntries");
            entity.HasKey(entry => entry.Id);
            entity.Property(entry => entry.Title).HasMaxLength(TimelineEntry.MaximumTitleLength).IsRequired();
            entity.Property(entry => entry.Description).HasMaxLength(TimelineEntry.MaximumDescriptionLength);
            entity.Property(entry => entry.Source).HasConversion<int>();
            entity.Property(entry => entry.StartAtUtc).HasConversion(UtcTicksConverter);
            entity.Property(entry => entry.EndAtUtc).HasConversion(NullableUtcTicksConverter);
            entity.Property(entry => entry.CreatedAtUtc).HasConversion(UtcTicksConverter);
            entity.Property(entry => entry.UpdatedAtUtc).HasConversion(UtcTicksConverter);
            entity.Ignore(entry => entry.IsRunning);
            entity.Ignore(entry => entry.ActiveTaskLinks);
            entity.Navigation(entry => entry.TaskLinks).UsePropertyAccessMode(PropertyAccessMode.Field);
            entity.HasIndex(entry => new { entry.IsDeleted, entry.StartAtUtc });
            entity.HasIndex(entry => new { entry.IsDeleted, entry.EndAtUtc });
            entity.HasMany(entry => entry.TaskLinks)
                .WithOne()
                .HasForeignKey(link => link.TimelineEntryId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TimelineEntryTaskLink>(entity =>
        {
            entity.ToTable("TimelineEntryTaskLinks");
            entity.HasKey(link => new { link.TimelineEntryId, link.TaskItemId });
            entity.HasIndex(link => new { link.TaskItemId, link.RemovedAtUtc });
            entity.Property(link => link.AddedAtUtc).HasConversion(UtcTicksConverter);
            entity.Property(link => link.RemovedAtUtc).HasConversion(NullableUtcTicksConverter);
            entity.Ignore(link => link.IsActive);
            entity.HasOne<TaskItem>()
                .WithMany()
                .HasForeignKey(link => link.TaskItemId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
