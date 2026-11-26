using System.Net.Http.Json;
using System.Text.Json;
using WebAPI.DTOs;

namespace WebAPI.Services;

public sealed class AssistantIngestionClient(HttpClient httpClient)
{
    private static readonly JsonSerializerOptions SnakeCaseJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public async Task<AssistantIndexResult> IndexAsync(AssistantIndexRequest request, CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync("/api/assistant/documents/index", request, SnakeCaseJsonOptions, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var detail = await response.Content.ReadAsStringAsync(cancellationToken);
            var message = string.IsNullOrWhiteSpace(detail) ? "Assistant indexing failed." : detail;
            throw new HttpRequestException(message, null, response.StatusCode);
        }
        return await response.Content.ReadFromJsonAsync<AssistantIndexResult>(SnakeCaseJsonOptions, cancellationToken)
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
