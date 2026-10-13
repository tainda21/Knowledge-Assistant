namespace WebAPI.DTOs;

public sealed record DocumentDto(
    Guid Id,
    string Name,
    string Content,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string SourceType,
    string? SourceFileName,
    string IngestionStatus,
    string? IngestionError,
    int ChunkCount,
    DateTimeOffset? IndexedAt
);

public sealed record SaveDocumentRequest(string Name, string Content);

public sealed record AssistantIndexRequest(
    string DocumentId,
    string Name,
    string SourceFileName,
    string? ContentType,
    string? Content,
    string? SourceBase64
);

public sealed record AssistantIndexResult(string ExtractedText, string ContentHash, int ChunkCount);
