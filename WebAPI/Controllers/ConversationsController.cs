using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebAPI.Data;
using WebAPI.DTOs;
using WebAPI.Models;

namespace WebAPI.Controllers;

[ApiController]
[Route("api/conversations")]
[Authorize]
public sealed class ConversationsController(AppDbContext db) : ControllerBase
{
    private Guid? CurrentUserId => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
    private bool IsAdmin => User.IsInRole("Admin");

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ConversationSummaryResponse>>> List(CancellationToken cancellationToken)
    {
        if (CurrentUserId is not { } userId)
        {
            return Unauthorized();
        }

        var query = db.Conversations.AsNoTracking();
        // Return current user's conversations
        query = query.Where(c => c.UserId == userId);

        var conversations = await query
            .OrderByDescending(c => c.UpdatedAt)
            .Select(c => new ConversationSummaryResponse(
                c.Id,
                c.CreatedAt,
                c.UpdatedAt,
                c.Messages.OrderBy(m => m.CreatedAt).Select(m => m.Content).FirstOrDefault()
            ))
            .ToListAsync(cancellationToken);

        return Ok(conversations);
    }

    [HttpPost]
    public async Task<ActionResult<CreateConversationResponse>> Create(CancellationToken cancellationToken)
    {
        if (CurrentUserId is not { } userId)
        {
            return Unauthorized();
        }

        var now = DateTimeOffset.UtcNow;
        var conversation = new Conversation
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CreatedAt = now,
            UpdatedAt = now
        };

        db.Conversations.Add(conversation);
        await db.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(Messages), new { id = conversation.Id }, new CreateConversationResponse(conversation.Id));
    }

    [HttpGet("{id:guid}/messages")]
    public async Task<ActionResult<IReadOnlyList<ChatMessageResponse>>> Messages(Guid id, CancellationToken cancellationToken)
    {
        if (CurrentUserId is not { } userId)
        {
            return Unauthorized();
        }

        var conversation = await db.Conversations
            .AsNoTracking()
            .SingleOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (conversation is null)
        {
            return NotFound();
        }

        if (conversation.UserId != userId && !IsAdmin)
        {
            return Forbid();
        }

        var messages = await db.ChatMessages
            .Where(message => message.ConversationId == id)
            .OrderBy(message => message.CreatedAt)
            .Select(message => new ChatMessageResponse(message.Id, message.Role, message.Content, message.CreatedAt))
            .ToListAsync(cancellationToken);

        return Ok(messages);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        if (CurrentUserId is not { } userId)
        {
            return Unauthorized();
        }

        var conversation = await db.Conversations
            .SingleOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (conversation is null)
        {
            return NotFound();
        }

        if (conversation.UserId != userId && !IsAdmin)
        {
            return Forbid();
        }

        db.Conversations.Remove(conversation);
        await db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }
}
