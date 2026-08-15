namespace AURA.Api.Correlation;

public sealed class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var incoming = context.Request.Headers[CorrelationIdResolver.HeaderName].FirstOrDefault();
        var correlationId = CorrelationIdResolver.Resolve(incoming);

        context.Items[CorrelationIdResolver.HeaderName] = correlationId;
        context.Response.Headers[CorrelationIdResolver.HeaderName] = correlationId;

        using (logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
        {
            await next(context);
        }
    }
}
