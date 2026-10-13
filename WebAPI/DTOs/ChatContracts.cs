using System.Text.Json.Serialization;

namespace WebAPI.DTOs;

public sealed record CreateConversationResponse(Guid ConversationId);

public sealed record ConversationSummaryResponse(
    Guid Id,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string? Snippet
);

public sealed record SendChatRequest(
    Guid? ConversationId,
    string Message,
    IReadOnlyList<ChatDocument>? Documents
);

public sealed record ChatMessageResponse(
    Guid Id,
    string Role,
    string Content,
    DateTimeOffset CreatedAt
);

public sealed record AssistantChatRequest(
    string Query,
    [property: JsonPropertyName("chat_history")]
    IReadOnlyList<AssistantHistoryMessage> ChatHistory,
    IReadOnlyList<ChatDocument> Documents
);

public sealed record AssistantHistoryMessage(
    string Role,
    string Content
);

public sealed record ChatDocument(
    string Name,
    string Content
);