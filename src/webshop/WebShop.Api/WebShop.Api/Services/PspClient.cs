using Common.Contracts;

namespace WebShop.Api.Services;

public sealed class PspClient
{
    private readonly HttpClient _http;

    public PspClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<PspInitResponse> InitAsync(PspInitRequest request, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync("/api/psp/transactions/init", request, ct);
        resp.EnsureSuccessStatusCode();

        var body = await resp.Content.ReadFromJsonAsync<PspInitResponse>(cancellationToken: ct);
        return body ?? throw new InvalidOperationException("PSP returned empty response.");
    }
}
