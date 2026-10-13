using System.Net;
using Microsoft.EntityFrameworkCore;
using WebAPI.Data;
using WebAPI.DTOs;

namespace WebAPI.Services;

public sealed class DocumentIngestionWorker(IServiceScopeFactory scopeFactory, ILogger<DocumentIngestionWorker> logger) : BackgroundService
{
    private const int MaxAttempts = 3;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await ProcessOneAsync(stoppingToken); }
            catch (Exception error) { logger.LogError(error, "Document ingestion worker failed."); }
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }

    private async Task ProcessOneAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = DateTimeOffset.UtcNow;
        var document = await db.KnowledgeDocuments.OrderBy(item => item.UpdatedAt).FirstOrDefaultAsync(
            item => item.IngestionStatus == "Deleting" ||
                (item.IngestionStatus == "Pending") ||
                (item.IngestionStatus == "Failed" && item.IngestionAttempts < MaxAttempts && item.NextAttemptAt <= now),
            cancellationToken);
        if (document is null) return;

        if (document.IngestionStatus == "Deleting")
        {
            try
            {
                await scope.ServiceProvider.GetRequiredService<AssistantIngestionClient>().DeleteAsync(document.Id, cancellationToken);
                db.KnowledgeDocuments.Remove(document);
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (Exception error)
            {
                document.IngestionError = error.Message;
                await db.SaveChangesAsync(cancellationToken);
            }
            return;
        }

        document.IngestionStatus = "Processing";
        document.IngestionError = null;
        await db.SaveChangesAsync(cancellationToken);
        try
        {
            var source = document.SourceData;
            var result = await scope.ServiceProvider.GetRequiredService<AssistantIngestionClient>().IndexAsync(
                new AssistantIndexRequest(document.Id.ToString(), document.Name, document.SourceFileName ?? $"{document.Name}.txt", document.ContentType, source is null ? document.Content : null, source is null ? null : Convert.ToBase64String(source)),
                cancellationToken);
            document.ExtractedText = result.ExtractedText;
            document.ContentHash = result.ContentHash;
            document.ChunkCount = result.ChunkCount;
            document.IngestionStatus = "Indexed";
            document.IndexedAt = DateTimeOffset.UtcNow;
            document.IngestionAttempts = 0;
            document.NextAttemptAt = null;
        }
        catch (HttpRequestException error) when (error.StatusCode == HttpStatusCode.UnprocessableEntity)
        {
            document.IngestionAttempts = MaxAttempts;
            document.IngestionStatus = "Failed";
            document.IngestionError = error.Message[..Math.Min(error.Message.Length, 2_000)];
            document.NextAttemptAt = null;
        }
        catch (Exception error)
        {
            document.IngestionAttempts++;
            document.IngestionStatus = "Failed";
            document.IngestionError = error.Message[..Math.Min(error.Message.Length, 2_000)];
            document.NextAttemptAt = document.IngestionAttempts < MaxAttempts ? DateTimeOffset.UtcNow.AddMinutes(document.IngestionAttempts) : null;
        }
        await db.SaveChangesAsync(cancellationToken);
    }
}
