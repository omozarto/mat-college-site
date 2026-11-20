using MathCollegeSiteBack.Models;
using Microsoft.EntityFrameworkCore;

namespace MathCollegeSiteBack;

public sealed class AppDbContext(
    DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<LearningLesson> Lessons => Set<LearningLesson>();
    public DbSet<CourseTopic> Topics => Set<CourseTopic>();
    public DbSet<LessonTask> LessonTasks => Set<LessonTask>();
    public DbSet<TaskOption> TaskOptions => Set<TaskOption>();
    public DbSet<LessonRun> LessonRuns => Set<LessonRun>();
    public DbSet<TaskAnswer> TaskAnswers => Set<TaskAnswer>();
    public DbSet<TeacherStreak> TeacherStreaks => Set<TeacherStreak>();
    public DbSet<StreakRecovery> StreakRecoveries => Set<StreakRecovery>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CourseTopic>()
            .Property(x => x.Name)
            .HasMaxLength(200);

        modelBuilder.Entity<CourseTopic>()
            .Property(x => x.Slug)
            .HasMaxLength(100);

        modelBuilder.Entity<CourseTopic>()
            .Property(x => x.Description)
            .HasMaxLength(1000);

        modelBuilder.Entity<CourseTopic>()
            .HasIndex(x => x.Slug)
            .IsUnique();

        modelBuilder.Entity<CourseTopic>()
            .HasIndex(x => x.Order)
            .IsUnique();

        modelBuilder.Entity<LearningLesson>()
            .HasOne(x => x.Topic)
            .WithMany(x => x.Lessons)
            .HasForeignKey(x => x.TopicId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<LearningLesson>()
            .Property(x => x.Title)
            .HasMaxLength(200);

        modelBuilder.Entity<LearningLesson>()
            .Property(x => x.Summary)
            .HasMaxLength(1000);

        modelBuilder.Entity<LearningLesson>()
            .Property(x => x.Content)
            .HasColumnType("text");

        modelBuilder.Entity<LearningLesson>()
            .HasIndex(x => new { x.TopicId, x.Order })
            .IsUnique();

        modelBuilder.Entity<LessonTask>()
            .HasIndex(x => new { x.LessonId, x.Order })
            .IsUnique();

        modelBuilder.Entity<LessonTask>()
            .HasOne(x => x.Lesson)
            .WithMany(x => x.Tasks)
            .HasForeignKey(x => x.LessonId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<LessonTask>()
            .Property(x => x.Points)
            .HasPrecision(6, 2);

        modelBuilder.Entity<LessonTask>()
            .Property(x => x.CorrectNumber)
            .HasPrecision(18, 6);

        modelBuilder.Entity<LessonTask>()
            .Property(x => x.NumericTolerance)
            .HasPrecision(18, 6);

        modelBuilder.Entity<TaskOption>()
            .Property(x => x.Text)
            .HasMaxLength(500);

        modelBuilder.Entity<LessonRun>()
            .Property(x => x.Score)
            .HasPrecision(8, 2);

        modelBuilder.Entity<LessonRun>()
            .Property(x => x.MaximumScore)
            .HasPrecision(8, 2);

        modelBuilder.Entity<LessonRun>()
            .Property(x => x.RatingPercent)
            .HasPrecision(5, 2);

        modelBuilder.Entity<TaskAnswer>()
            .Property(x => x.SubmittedNumericAnswer)
            .HasPrecision(18, 6);

        modelBuilder.Entity<LessonRun>()
            .HasIndex(x => new { x.LessonId, x.AbandonedAtUtc });

        modelBuilder.Entity<LessonRun>()
            .HasIndex(x => new { x.LessonId, x.CompletedAtUtc });

        modelBuilder.Entity<LessonRun>()
            .HasOne(x => x.Lesson)
            .WithMany()
            .HasForeignKey(x => x.LessonId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<TaskAnswer>()
            .HasOne(x => x.LessonRun)
            .WithMany()
            .HasForeignKey(x => x.LessonRunId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<TaskAnswer>()
            .HasOne(x => x.LessonTask)
            .WithMany()
            .HasForeignKey(x => x.LessonTaskId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<TaskAnswer>()
            .HasOne<TaskOption>()
            .WithMany()
            .HasForeignKey(x => x.SelectedOptionId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<TaskAnswer>()
            .HasIndex(x => new
            {
                x.LessonRunId,
                x.LessonTaskId,
                x.AttemptNumber
            })
            .IsUnique();

        modelBuilder.Entity<StreakRecovery>()
            .HasIndex(x => x.UsedOn);
    }
}