using System.Text;
using System.Text.Json;
using Common.Contracts;
using Common.Observability;
using Common.Security;
using Microsoft.AspNetCore.Http;
using Common.Contracts;
namespace Psp.Api.Services;

public sealed class BankClient
{
    private readonly HttpClient _http;
    private readonly IConfiguration _cfg;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public BankClient(HttpClient http, IConfiguration cfg, IHttpContextAccessor httpContextAccessor)
    {
        _http = http;
        _cfg = cfg;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<BankInitResponse> InitPaymentAsync(BankInitRequest request, CancellationToken ct = default)
    {
        var secret = _cfg["Hmac:PspBankSecret"];
        if (string.IsNullOrWhiteSpace(secret))
            throw new InvalidOperationException("Missing Hmac:PspBankSecret configuration.");

        var path = "/api/bank/payments/init";
        var timestamp = HmacSigner.CreateTimestampUtc();

        var json = JsonSerializer.Serialize(request, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        var canonical = HmacSigner.BuildCanonical(timestamp, "POST", path, json);
        var signature = HmacSigner.ComputeSignatureBase64(secret, canonical);

        using var httpReq = new HttpRequestMessage(HttpMethod.Post, path);
        httpReq.Content = new StringContent(json, Encoding.UTF8, "application/json");

        httpReq.Headers.Add(HmacSigner.TimestampHeader, timestamp);
        httpReq.Headers.Add(HmacSigner.SignatureHeader, signature);

        // Trace propagation
        var ctx = _httpContextAccessor.HttpContext;
        if (ctx is not null)
        {
            var cid = CorrelationIdMiddleware.Get(ctx);
            httpReq.Headers.TryAddWithoutValidation(CorrelationIdMiddleware.HeaderName, cid);
        }

        using var resp = await _http.SendAsync(httpReq, ct);
        resp.EnsureSuccessStatusCode();

        var body = await resp.Content.ReadAsStringAsync(ct);
        var result = JsonSerializer.Deserialize<BankInitResponse>(body, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        return result ?? throw new InvalidOperationException("Bank returned empty response.");
    }

    public async Task<BankPaymentStatusResponse> GetInternalStatusAsync(Guid paymentId, CancellationToken ct = default)
    {
        var secret = _cfg["Hmac:PspBankSecret"];
        if (string.IsNullOrWhiteSpace(secret))
            throw new InvalidOperationException("Missing Hmac:PspBankSecret configuration.");

        var path = $"/api/bank/internal/payments/{paymentId}";
        var timestamp = HmacSigner.CreateTimestampUtc();

        // GET has an empty body
        var canonical = HmacSigner.BuildCanonical(timestamp, "GET", path, "");
        var signature = HmacSigner.ComputeSignatureBase64(secret, canonical);

        using var httpReq = new HttpRequestMessage(HttpMethod.Get, path);
        httpReq.Headers.Add(HmacSigner.TimestampHeader, timestamp);
        httpReq.Headers.Add(HmacSigner.SignatureHeader, signature);

        var ctx = _httpContextAccessor.HttpContext;
        if (ctx is not null)
        {
            var cid = CorrelationIdMiddleware.Get(ctx);
            httpReq.Headers.TryAddWithoutValidation(CorrelationIdMiddleware.HeaderName, cid);
        }

        using var resp = await _http.SendAsync(httpReq, ct);
        resp.EnsureSuccessStatusCode();

        var body = await resp.Content.ReadAsStringAsync(ct);
        var result = JsonSerializer.Deserialize<BankPaymentStatusResponse>(body, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        return result ?? throw new InvalidOperationException("Bank returned empty response.");
    }
}
