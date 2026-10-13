using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebAPI.Data;
using WebAPI.DTOs;
using WebAPI.Models;

namespace WebAPI.Controllers;

[ApiController]
[Route("api/documents")]
[Authorize(Roles = "Admin")]
public sealed class DocumentsController(AppDbContext db) : ControllerBase
{
    private const int MaxNameLength = 255;
    private const int MaxContentLength = 100_000;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DocumentDto>>> GetAll(CancellationToken cancellationToken)
    {
        return Ok(await db.KnowledgeDocuments
            .OrderByDescending(document => document.UpdatedAt)
            .Select(document => new DocumentDto(
                document.Id, document.Name, document.Content, document.CreatedAt, document.UpdatedAt,
                document.SourceType, document.SourceFileName, document.IngestionStatus, document.IngestionError,
                document.ChunkCount, document.IndexedAt))
            .ToListAsync(cancellationToken));
    }

    [HttpPost]
    public async Task<ActionResult<DocumentDto>> Create(SaveDocumentRequest request, CancellationToken cancellationToken)
    {
        var validation = Validate(request, out var name, out var content);
        if (validation is not null) return BadRequest(new { message = validation });
        var now = DateTimeOffset.UtcNow;
        var document = new KnowledgeDocument
        {
            Id = Guid.NewGuid(),
            Name = name,
            Content = content,
            SourceType = "Text",
            ContentType = "text/plain",
            SourceFileName = $"{name}.txt",
            IngestionStatus = "Pending",
            CreatedAt = now,
            UpdatedAt = now
        };
        db.KnowledgeDocuments.Add(document);
        await db.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(GetAll), new { id = document.Id }, ToDto(document));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<DocumentDto>> Update(Guid id, SaveDocumentRequest request, CancellationToken cancellationToken)
    {
        var document = await db.KnowledgeDocuments.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (document is null) return NotFound(new { message = "Document not found." });
        var validation = Validate(request, out var name, out var content);
        if (validation is not null) return BadRequest(new { message = validation });
        document.Name = name;
        document.Content = content;
        document.SourceType = "Text";
        document.ContentType = "text/plain";
        document.SourceFileName = $"{name}.txt";
        document.SourceData = null;
        document.IngestionStatus = "Pending";
        document.IngestionError = null;
        document.IngestionAttempts = 0;
        document.NextAttemptAt = null;
        document.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(ToDto(document));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var document = await db.KnowledgeDocuments.FindAsync([id], cancellationToken);
        if (document is null) return NotFound(new { message = "Document not found." });
        document.IngestionStatus = "Deleting";
        document.IngestionError = null;
        await db.SaveChangesAsync(cancellationToken);
        return Accepted();
    }

    [HttpPost("upload")]
    [RequestSizeLimit(10_000_000)]
    public async Task<ActionResult<DocumentDto>> Upload([FromForm] IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0) return BadRequest(new { message = "A non-empty document file is required." });
        if (file.Length > 10_000_000) return BadRequest(new { message = "Document files must be 10 MB or smaller." });
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension is not ".pdf" and not ".txt" and not ".md" and not ".markdown")
            return BadRequest(new { message = "Only PDF, TXT, Markdown, and .markdown files are supported." });
        await using var stream = new MemoryStream();
        await file.CopyToAsync(stream, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var document = new KnowledgeDocument
        {
            Id = Guid.NewGuid(),
            Name = Path.GetFileNameWithoutExtension(file.FileName),
            Content = string.Empty,
            SourceType = "File",
            ContentType = file.ContentType,
            SourceFileName = Path.GetFileName(file.FileName),
            SourceData = stream.ToArray(),
            IngestionStatus = "Pending",
            CreatedAt = now,
            UpdatedAt = now
        };
        db.KnowledgeDocuments.Add(document);
        await db.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(GetAll), new { id = document.Id }, ToDto(document));
    }

    [HttpPost("{id:guid}/retry")]
    public async Task<ActionResult<DocumentDto>> Retry(Guid id, CancellationToken cancellationToken)
    {
        var document = await db.KnowledgeDocuments.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (document is null) return NotFound(new { message = "Document not found." });
        if (document.IngestionStatus != "Failed") return BadRequest(new { message = "Only failed documents can be retried." });
        document.IngestionStatus = "Pending";
        document.IngestionError = null;
        document.IngestionAttempts = 0;
        document.NextAttemptAt = null;
        document.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(ToDto(document));
    }

    private static string? Validate(SaveDocumentRequest request, out string name, out string content)
    {
        name = request.Name?.Trim() ?? string.Empty;
        content = request.Content?.Trim() ?? string.Empty;
        if (name.Length is < 1 or > MaxNameLength) return "Document name must contain 1 to 255 characters.";
        if (content.Length is < 1 or > MaxContentLength) return "Document content must contain 1 to 100000 characters.";
        return null;
    }

    private static DocumentDto ToDto(KnowledgeDocument document) =>
        new(document.Id, document.Name, document.Content, document.CreatedAt, document.UpdatedAt,
            document.SourceType, document.SourceFileName, document.IngestionStatus, document.IngestionError,
            document.ChunkCount, document.IndexedAt);
}
