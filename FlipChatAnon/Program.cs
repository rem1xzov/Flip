using ChatAnon.Hubs;
using ChatAnon.Middleware;
using Serilog;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.RateLimiting;

// Настройка Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/chat-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);

// Добавляем Serilog
builder.Host.UseSerilog();

// Kestrel hardening
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 1024 * 1024; // 1 MB
    options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(15);
    options.Limits.KeepAliveTimeout = TimeSpan.FromSeconds(60);
});

// Настройка CORS для безопасности
builder.Services.AddCors(options =>
{
    options.AddPolicy("ChatPolicy", policy =>
    {
        policy
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials()
            .SetIsOriginAllowed(origin =>
            {
                // Разрешаем file:// (Origin = null)
                if (string.IsNullOrEmpty(origin)) return true;
                try
                {
                    var uri = new Uri(origin);
                    return uri.Host == "localhost" || uri.Host == "127.0.0.1";
                }
                catch
                {
                    return false;
                }
            });
    });
});

// Настройка Rate Limiting
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("ChatPolicy", limiterOptions =>
    {
        limiterOptions.PermitLimit = 100;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst;
        limiterOptions.QueueLimit = 10;
    });
    
    options.AddTokenBucketLimiter("MessagePolicy", limiterOptions =>
    {
        limiterOptions.TokenLimit = 30;
        limiterOptions.QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst;
        limiterOptions.QueueLimit = 5;
        limiterOptions.ReplenishmentPeriod = TimeSpan.FromMinutes(1);
        limiterOptions.TokensPerPeriod = 30;
        limiterOptions.AutoReplenishment = true;
    });
});

// Настройка SignalR с ограничениями
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.MaximumReceiveMessageSize = 1024 * 4; // 4KB максимум на сообщение
    options.StreamBufferCapacity = 10;
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
    options.HandshakeTimeout = TimeSpan.FromSeconds(15);
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    options.MaximumParallelInvocationsPerClient = 1;
});

// Настройка контроллеров с валидацией
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.JsonSerializerOptions.WriteIndented = true;
    });

builder.Services.AddEndpointsApiExplorer();

// Добавляем Memory Cache для временного хранения
builder.Services.AddMemoryCache();

// Forwarded headers (behind reverse proxy)
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

// Настройка middleware pipeline
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

// Security Headers
app.Use(async (context, next) =>
{
    context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Add("X-Frame-Options", "DENY");
    context.Response.Headers.Add("X-XSS-Protection", "1; mode=block");
    context.Response.Headers.Add("Referrer-Policy", "strict-origin-when-cross-origin");
    context.Response.Headers.Add("Permissions-Policy", "geolocation=(), microphone=(), camera=()");
    context.Response.Headers.Add("Content-Security-Policy", "default-src 'self'; script-src 'self' 'unsafe-inline' https://cdn.tailwindcss.com https://unpkg.com https://cdn.jsdelivr.net; style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; font-src 'self' https://fonts.gstatic.com; connect-src 'self' http: https: ws: wss:;");
    context.Response.Headers.Add("Strict-Transport-Security", "max-age=31536000; includeSubDomains");
    await next();
});

// Rate Limiting
app.UseRateLimiter();

app.UseForwardedHeaders();
// В Dev разрешаем работать по HTTP (для file:// фронта)
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
// Кастомный middleware для логирования
app.UseMiddleware<RequestLoggingMiddleware>();

app.UseRouting();

// CORS должен быть после UseRouting и до MapHub/MapControllers
app.UseCors("ChatPolicy");

app.UseAuthorization();

// Настройка SignalR Hub с привязанной CORS политикой
app.MapHub<ChatHub>("/chat").RequireCors("ChatPolicy");

app.MapControllers().RequireCors("ChatPolicy");

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Timestamp = DateTime.UtcNow }));

try
{
    Log.Information("Starting ChatAnon application");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}