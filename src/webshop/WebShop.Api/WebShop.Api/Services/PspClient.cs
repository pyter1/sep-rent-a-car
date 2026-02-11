using System.Text;
using System.Text.Json;
using Common.Contracts;
using Common.Observability;
using Common.Security;
using Microsoft.AspNetCore.Http;

namespace WebShop.Api.Services;

public sealed class PspClient
{
    private readonly HttpClient _http;
    private readonly IConfiguration _cfg;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public PspClient(HttpClient http, IConfiguration cfg, IHttpContextAccessor httpContextAccessor)
    {
        _http = http;
        _cfg = cfg;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<PspInitResponse> InitAsync(PspInitRequest request, CancellationToken ct = default)
    {
        var secret = _cfg["Hmac:WebShopPspSecret"];
        if (string.IsNullOrWhiteSpace(secret))
            throw new InvalidOperationException("Missing Hmac:WebShopPspSecret configuration.");

        var path = "/api/psp/transactions/init";
        var timestamp = HmacSigner.CreateTimestampUtc();

        var json = JsonSerializer.Serialize(request, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        var canonical = HmacSigner.BuildCanonical(timestamp, "POST", path, json);
        var signature = HmacSigner.ComputeSignatureBase64(secret, canonical);

        using var httpReq = new HttpRequestMessage(HttpMethod.Post, path);
        httpReq.Content = new StringContent(json, Encoding.UTF8, "application/json");

        // HMAC integrity + replay defense inputs
        httpReq.Headers.Add(HmacSigner.TimestampHeader, timestamp);
        httpReq.Headers.Add(HmacSigner.SignatureHeader, signature);

        // Trace propagation (optional but recommended)
        var ctx = _httpContextAccessor.HttpContext;
        if (ctx is not null)
        {
            var cid = CorrelationIdMiddleware.Get(ctx);
            httpReq.Headers.TryAddWithoutValidation(CorrelationIdMiddleware.HeaderName, cid);
        }

        using var resp = await _http.SendAsync(httpReq, ct);
        resp.EnsureSuccessStatusCode();

        var body = await resp.Content.ReadAsStringAsync(ct);
        var result = JsonSerializer.Deserialize<PspInitResponse>(body, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        return result ?? throw new InvalidOperationException("PSP returned empty response.");
    }
}
