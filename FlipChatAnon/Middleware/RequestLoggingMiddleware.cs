using System.Diagnostics;
using Serilog;

namespace ChatAnon.Middleware;

public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;

    public RequestLoggingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var requestId = Guid.NewGuid().ToString("N")[..8];
        
        // Добавляем request ID в заголовки для трассировки
        context.Response.Headers.Add("X-Request-ID", requestId);
        
        Log.Information("Request started: {Method} {Path} from {RemoteIP} [RequestId: {RequestId}]",
            context.Request.Method,
            context.Request.Path,
            context.Connection.RemoteIpAddress?.ToString(),
            requestId);

        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Request failed: {Method} {Path} - {Error} [RequestId: {RequestId}]",
                context.Request.Method,
                context.Request.Path,
                ex.Message,
                requestId);
            throw;
        }
        finally
        {
            stopwatch.Stop();
            
            Log.Information("Request completed: {Method} {Path} - {StatusCode} in {ElapsedMs}ms [RequestId: {RequestId}]",
                context.Request.Method,
                context.Request.Path,
                context.Response.StatusCode,
                stopwatch.ElapsedMilliseconds,
                requestId);
        }
    }
}