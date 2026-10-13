using Microsoft.EntityFrameworkCore;
using WebAPI.Models;

namespace WebAPI.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<KnowledgeDocument> KnowledgeDocuments => Set<KnowledgeDocument>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(user => user.Id);
            entity.Property(user => user.Username).HasMaxLength(64).IsRequired();
            entity.HasIndex(user => user.Username).IsUnique();
            entity.Property(user => user.PasswordHash).IsRequired();
            entity.Property(user => user.Salt).IsRequired();
            entity.Property(user => user.Role).HasMaxLength(32).IsRequired();
            entity.Property(user => user.CreatedAt)
                .HasConversion(value => value.UtcDateTime, value => new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc)))
                .IsRequired();
            entity.HasMany(user => user.Conversations)
                .WithOne(conversation => conversation.User)
                .HasForeignKey(conversation => conversation.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Conversation>(entity =>
        {
            entity.HasKey(conversation => conversation.Id);
            entity.Property(conversation => conversation.CreatedAt)
                .HasConversion(value => value.UtcDateTime, value => new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc)))
                .IsRequired();
            entity.Property(conversation => conversation.UpdatedAt)
                .HasConversion(value => value.UtcDateTime, value => new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc)))
                .IsRequired();
            entity.HasIndex(conversation => conversation.UserId);
            entity.HasMany(conversation => conversation.Messages)
                .WithOne(message => message.Conversation)
                .HasForeignKey(message => message.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ChatMessage>(entity =>
        {
            entity.HasKey(message => message.Id);
            entity.Property(message => message.Role).HasMaxLength(16).IsRequired();
            entity.Property(message => message.Content).HasMaxLength(8000).IsRequired();
            entity.Property(message => message.CreatedAt)
                .HasConversion(value => value.UtcDateTime, value => new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc)))
                .IsRequired();
            entity.HasIndex(message => new { message.ConversationId, message.CreatedAt });
        });

        modelBuilder.Entity<KnowledgeDocument>(entity =>
        {
            entity.HasKey(document => document.Id);
            entity.Property(document => document.Name).HasMaxLength(255).IsRequired();
            entity.Property(document => document.Content).HasMaxLength(100_000).IsRequired();
            entity.Property(document => document.SourceType).HasMaxLength(32).IsRequired();
            entity.Property(document => document.ContentType).HasMaxLength(128);
            entity.Property(document => document.SourceFileName).HasMaxLength(255);
            entity.Property(document => document.IngestionStatus).HasMaxLength(32).IsRequired();
            entity.Property(document => document.IngestionError).HasMaxLength(2_000);
            entity.Property(document => document.ContentHash).HasMaxLength(64);
            entity.Property(document => document.CreatedAt)
                .HasConversion(value => value.UtcDateTime, value => new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc)))
                .IsRequired();
            entity.Property(document => document.UpdatedAt)
                .HasConversion(value => value.UtcDateTime, value => new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc)))
                .IsRequired();
            entity.HasIndex(document => document.UpdatedAt);
            entity.HasIndex(document => new { document.IngestionStatus, document.NextAttemptAt });
        });
    }
}
