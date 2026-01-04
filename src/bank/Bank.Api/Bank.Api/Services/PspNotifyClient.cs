using Common.Contracts;

namespace Bank.Api.Services;

public sealed class PspNotifyClient
{
    private readonly HttpClient _http;

    public PspNotifyClient(HttpClient http)
    {
        _http = http;
    }

    public async Task NotifyAsync(PspBankNotifyRequest request, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync("/api/psp/bank/notify", request, ct);
        resp.EnsureSuccessStatusCode();
    }
}
