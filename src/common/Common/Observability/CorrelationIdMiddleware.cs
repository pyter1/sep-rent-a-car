using Microsoft.AspNetCore.Http;

namespace Common.Observability;

public sealed class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-Id";
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

    public async Task Invoke(HttpContext context)
    {
        var correlationId = context.Request.Headers.TryGetValue(HeaderName, out var cid) && !string.IsNullOrWhiteSpace(cid)
            ? cid.ToString()
            : Guid.NewGuid().ToString("D");

        context.Items[HeaderName] = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        await _next(context);
    }

    public static string Get(HttpContext ctx)
        => (ctx.Items.TryGetValue(HeaderName, out var v) ? v?.ToString() : null)
           ?? Guid.NewGuid().ToString("D");
}
