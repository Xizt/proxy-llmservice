using System.Threading.RateLimiting;
using FluentValidation;
using FluentValidation.AspNetCore;
using LlmShadow.Common.Extensions;
using LlmShadow.DataLayer.Common;
using LlmShadow.DataLayer.Extensions;
using LlmShadow.Inference.Extensions;
using LlmShadow.Messaging.Extensions;
using LlmShadow.Models.Request;
using LlmShadow.ProxyService.BusinessLayer;
using LlmShadow.ProxyService.Middleware;
using LlmShadow.ProxyService.Validators;
using Microsoft.AspNetCore.RateLimiting;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting LlmShadow.ProxyService");

    var builder = WebApplication.CreateBuilder(args);

    // ── Serilog ───────────────────────────────────────────────────────────────
    builder.Host.UseSerilog((ctx, _, cfg) =>
        cfg.ReadFrom.Configuration(ctx.Configuration)
           .WriteTo.Console());

    // ── Core services ─────────────────────────────────────────────────────────
    builder.Services.AddCommonServices(builder.Configuration);
    builder.Services.AddDataLayer();
    builder.Services.AddInferenceClient();
    builder.Services.AddMessaging();

    // ── Business layer ────────────────────────────────────────────────────────
    builder.Services.AddScoped<IChatProxyService, ChatProxyService>();

    // ── Validation ────────────────────────────────────────────────────────────
    builder.Services.AddFluentValidationAutoValidation();
    builder.Services.AddScoped<IValidator<ChatRequestDto>, ChatRequestValidator>();

    // ── Controllers & Swagger ─────────────────────────────────────────────────
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new() { Title = "LLM Shadow Proxy API", Version = "v1" });
    });

    // ── Health checks ─────────────────────────────────────────────────────────
    builder.Services.AddHealthChecks()
        .AddDbContextCheck<LlmShadow.DataLayer.ShadowDbContext>("database");

    // ── Rate limiting ─────────────────────────────────────────────────────────
    var permitLimit = builder.Configuration.GetValue("RateLimiting:PermitLimit", 100);
    var windowSeconds = builder.Configuration.GetValue("RateLimiting:WindowSeconds", 10);

    builder.Services.AddRateLimiter(opts =>
    {
        opts.AddFixedWindowLimiter("fixed", limiter =>
        {
            limiter.PermitLimit = permitLimit;
            limiter.Window = TimeSpan.FromSeconds(windowSeconds);
            limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
            limiter.QueueLimit = 20;
        });
        opts.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    });

    // ── Response compression ──────────────────────────────────────────────────
    builder.Services.AddResponseCompression(opts => opts.EnableForHttps = true);

    // ── Security headers ──────────────────────────────────────────────────────
    builder.Services.AddHsts(opts =>
    {
        opts.MaxAge = TimeSpan.FromDays(365);
        opts.IncludeSubDomains = true;
    });

    var app = builder.Build();

    // ── Apply DB migrations ───────────────────────────────────────────────────
    using (var scope = app.Services.CreateScope())
    {
        var migrator = scope.ServiceProvider.GetRequiredService<DatabaseMigrator>();
        await migrator.MigrateAsync();
    }

    // ── Middleware pipeline ───────────────────────────────────────────────────
    app.UseMiddleware<ExceptionMiddleware>();
    app.UseMiddleware<CorrelationIdMiddleware>();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseHttpsRedirection();
    app.UseHsts();
    app.UseResponseCompression();
    app.UseRateLimiter();

    app.MapControllers();
    app.MapHealthChecks("/health");

    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "LlmShadow.ProxyService terminated unexpectedly");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}
