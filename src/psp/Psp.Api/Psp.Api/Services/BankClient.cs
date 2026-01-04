using Common.Contracts;

namespace Psp.Api.Services;

public sealed class BankClient
{
    private readonly HttpClient _http;

    public BankClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<BankInitResponse> InitPaymentAsync(BankInitRequest request, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync("/api/bank/payments/init", request, ct);
        resp.EnsureSuccessStatusCode();

        var body = await resp.Content.ReadFromJsonAsync<BankInitResponse>(cancellationToken: ct);
        return body ?? throw new InvalidOperationException("Bank returned empty response.");
    }
}
