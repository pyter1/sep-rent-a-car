using System.Text;

namespace WebShop.Api.Middleware;

public sealed class RequestBodyCaptureMiddleware
{
    public const string RawBodyItemKey = "raw_body";

    private readonly RequestDelegate _next;

    public RequestBodyCaptureMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task Invoke(HttpContext ctx)
    {
        if (HttpMethods.IsPost(ctx.Request.Method)
            || HttpMethods.IsPut(ctx.Request.Method)
            || HttpMethods.IsPatch(ctx.Request.Method))
        {
            ctx.Request.EnableBuffering();

            ctx.Request.Body.Position = 0;
            using var reader = new StreamReader(ctx.Request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
            var body = await reader.ReadToEndAsync();
            ctx.Items[RawBodyItemKey] = body;
            ctx.Request.Body.Position = 0;
        }

        await _next(ctx);
    }
}
