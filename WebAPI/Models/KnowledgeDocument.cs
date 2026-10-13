namespace WebAPI.Models;

public sealed class KnowledgeDocument
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string SourceType { get; set; } = "Text";
    public string? ContentType { get; set; }
    public string? SourceFileName { get; set; }
    public byte[]? SourceData { get; set; }
    public string? ExtractedText { get; set; }
    public string? ContentHash { get; set; }
    public int ChunkCount { get; set; }
    public string IngestionStatus { get; set; } = "Pending";
    public string? IngestionError { get; set; }
    public DateTimeOffset? IndexedAt { get; set; }
    public int IngestionAttempts { get; set; }
    public DateTimeOffset? NextAttemptAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
