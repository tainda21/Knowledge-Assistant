using System.Net.Http.Json;
using WebAPI.DTOs;

namespace WebAPI.Services;

public sealed class AssistantIngestionClient(HttpClient httpClient)
{
    public async Task<AssistantIndexResult> IndexAsync(AssistantIndexRequest request, CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync("/api/assistant/documents/index", request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var detail = await response.Content.ReadAsStringAsync(cancellationToken);
            var message = string.IsNullOrWhiteSpace(detail) ? "Assistant indexing failed." : detail;
            throw new HttpRequestException(message, null, response.StatusCode);
        }
        return await response.Content.ReadFromJsonAsync<AssistantIndexResult>(cancellationToken)
            ?? throw new InvalidOperationException("Assistant returned an invalid indexing response.");
    }

    public async Task DeleteAsync(Guid documentId, CancellationToken cancellationToken)
    {
        using var response = await httpClient.DeleteAsync($"/api/assistant/documents/{documentId}", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var detail = await response.Content.ReadAsStringAsync(cancellationToken);
            var message = string.IsNullOrWhiteSpace(detail) ? "Assistant deletion failed." : detail;
            throw new HttpRequestException(message, null, response.StatusCode);
        }
    }
}
