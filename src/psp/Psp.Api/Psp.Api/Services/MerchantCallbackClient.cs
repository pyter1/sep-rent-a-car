using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Common.Observability;
using Common.Security;

namespace Psp.Api.Services;

public sealed class MerchantCallbackClient
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly IConfiguration _cfg;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public MerchantCallbackClient(IHttpClientFactory httpFactory, IConfiguration cfg, IHttpContextAccessor httpContextAccessor)
    {
        _httpFactory = httpFactory;
        _cfg = cfg;
        _httpContextAccessor = httpContextAccessor;
    }

    private string ResolveSecret(string merchantId)
    {
        // Optional: per-merchant secret override
        var perMerchant = _cfg[$"Merchants:{merchantId}:HmacSecret"];
        if (!string.IsNullOrWhiteSpace(perMerchant)) return perMerchant;

        var global = _cfg["Hmac:WebShopPspSecret"];
        if (string.IsNullOrWhiteSpace(global))
            throw new InvalidOperationException("Missing Hmac:WebShopPspSecret configuration.");

        return global;
    }

    public async Task<HttpResponseMessage> PostSignedAsync<T>(
        string merchantId,
        string callbackUrl,
        T payload,
        CancellationToken ct)
    {
        var secret = ResolveSecret(merchantId);

        var uri = new Uri(callbackUrl);
        var pathAndQuery = uri.PathAndQuery;
        var timestamp = HmacSigner.CreateTimestampUtc();

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });

        var canonical = HmacSigner.BuildCanonical(timestamp, "POST", pathAndQuery, json);
        var signature = HmacSigner.ComputeSignatureBase64(secret, canonical);

        using var req = new HttpRequestMessage(HttpMethod.Post, callbackUrl)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        req.Headers.Add(HmacSigner.TimestampHeader, timestamp);
        req.Headers.Add(HmacSigner.SignatureHeader, signature);

        var ctx = _httpContextAccessor.HttpContext;
        if (ctx is not null)
        {
            var cid = CorrelationIdMiddleware.Get(ctx);
            req.Headers.TryAddWithoutValidation(CorrelationIdMiddleware.HeaderName, cid);
        }

        var client = _httpFactory.CreateClient("MerchantCallback");
        return await client.SendAsync(req, ct);
    }
}
