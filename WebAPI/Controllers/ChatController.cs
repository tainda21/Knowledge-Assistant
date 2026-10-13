using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebAPI.Data;
using WebAPI.DTOs;
using WebAPI.Models;
using WebAPI.Services;

namespace WebAPI.Controllers;

[ApiController]
[Route("api/chat")]
[Authorize]
public sealed class ChatController(AppDbContext db, AssistantServiceClient assistant) : ControllerBase
{
    private const int MaxMessageLength = 8_000;
    private const int MaxDocumentCount = 10;
    private const int MaxDocumentLength = 100_000;

    [HttpPost("stream")]
    public async Task Stream(SendChatRequest request, CancellationToken cancellationToken)
    {
        var currentUserIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(currentUserIdStr, out var currentUserId))
        {
            Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        var message = request.Message?.Trim();
        if (string.IsNullOrWhiteSpace(message) || message.Length > MaxMessageLength)
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            await Response.WriteAsJsonAsync(new { message = "Message must contain 1 to 8000 characters." }, cancellationToken);
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var conversation = request.ConversationId is Guid requestedId
            ? await db.Conversations.SingleOrDefaultAsync(item => item.Id == requestedId, cancellationToken)
            : null;

        if (request.ConversationId.HasValue && conversation is null)
        {
            Response.StatusCode = StatusCodes.Status404NotFound;
            await Response.WriteAsJsonAsync(new { message = "Conversation was not found." }, cancellationToken);
            return;
        }

        if (conversation is not null && conversation.UserId != currentUserId && !User.IsInRole("Admin"))
        {
            Response.StatusCode = StatusCodes.Status403Forbidden;
            await Response.WriteAsJsonAsync(new { message = "Access denied." }, cancellationToken);
            return;
        }

        if (conversation is null)
        {
            conversation = new Conversation { Id = Guid.NewGuid(), UserId = currentUserId, CreatedAt = now, UpdatedAt = now };
            db.Conversations.Add(conversation);
        }

        var history = await db.ChatMessages.Where(item => item.ConversationId == conversation.Id)
            .OrderBy(item => item.CreatedAt)
            .Select(item => new AssistantHistoryMessage(item.Role, item.Content)).ToListAsync(cancellationToken);
        var attachedDocuments = (request.Documents ?? [])
            .Where(document => !string.IsNullOrWhiteSpace(document.Name) && !string.IsNullOrWhiteSpace(document.Content))
            .Take(MaxDocumentCount)
            .Where(document => document.Content.Length <= MaxDocumentLength)
            .ToList();
        var documents = attachedDocuments;
        db.ChatMessages.Add(new ChatMessage { Id = Guid.NewGuid(), ConversationId = conversation.Id, Role = "user", Content = message, CreatedAt = now });
        conversation.UpdatedAt = now;
        await db.SaveChangesAsync(cancellationToken);

        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers["X-Conversation-Id"] = conversation.Id.ToString();
        await Response.StartAsync(cancellationToken);

        var answer = new System.Text.StringBuilder();
        var completed = false;
        await foreach (var item in assistant.StreamAsync(new AssistantChatRequest(message, history, documents), cancellationToken))
        {
            if (item.Event == "token" && item.Text is not null)
            {
                answer.Append(item.Text);
                await WriteEvent("token", new { text = item.Text }, cancellationToken);
            }
            else if (item.Event == "error")
            {
                await WriteEvent("error", new { message = item.Error ?? "The AI service failed." }, cancellationToken);
                return;
            }
            else if (item.Event == "complete")
            {
                completed = true;
            }
        }

        if (completed && answer.Length > 0 && !cancellationToken.IsCancellationRequested)
        {
            var completedAt = DateTimeOffset.UtcNow;
            db.ChatMessages.Add(new ChatMessage { Id = Guid.NewGuid(), ConversationId = conversation.Id, Role = "assistant", Content = answer.ToString(), CreatedAt = completedAt });
            conversation.UpdatedAt = completedAt;
            await db.SaveChangesAsync(cancellationToken);
            await WriteEvent("complete", new { }, cancellationToken);
        }
        else if (!cancellationToken.IsCancellationRequested)
        {
            await WriteEvent("error", new { message = "The AI service returned an empty response." }, cancellationToken);
        }
    }

    private async Task WriteEvent(string eventName, object payload, CancellationToken cancellationToken)
    {
        await Response.WriteAsync($"event: {eventName}\ndata: {JsonSerializer.Serialize(payload)}\n\n", cancellationToken);
        await Response.Body.FlushAsync(cancellationToken);
    }
}
