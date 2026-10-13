using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using WebAPI.DTOs;

namespace WebAPI.Services;

public sealed record AssistantStreamEvent(string Event, string? Text, string? Error);

public sealed class AssistantServiceClient(HttpClient httpClient)
{
    public async IAsyncEnumerable<AssistantStreamEvent> StreamAsync(
        AssistantChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, "/api/assistant/chat/stream")
        {
            Content = JsonContent.Create(request),
        };
        using var response = await httpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            yield return new AssistantStreamEvent("error", null, "The AI service is unavailable.");
            yield break;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        string? eventName = null;
        string? data = null;
        while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                break;
            }
            if (line.StartsWith("event: ", StringComparison.Ordinal))
            {
                eventName = line[7..];
            }
            else if (line.StartsWith("data: ", StringComparison.Ordinal))
            {
                data = line[6..];
            }
            else if (line.Length == 0 && eventName is not null && data is not null)
            {
                using var document = JsonDocument.Parse(data);
                var root = document.RootElement;
                yield return eventName switch
                {
                    "token" => new AssistantStreamEvent("token", root.GetProperty("text").GetString(), null),
                    "error" => new AssistantStreamEvent("error", null, root.GetProperty("message").GetString()),
                    _ => new AssistantStreamEvent("complete", null, null),
                };
                eventName = null;
                data = null;
            }
        }
    }
}
