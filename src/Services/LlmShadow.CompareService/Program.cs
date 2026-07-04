using LlmShadow.Common.Extensions;
using LlmShadow.CompareService.BusinessLayer;
using LlmShadow.CompareService.Middleware;
using LlmShadow.DataLayer.Common;
using LlmShadow.DataLayer.Extensions;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting LlmShadow.CompareService");

    var builder = WebApplication.CreateBuilder(args);

    // ── Serilog ───────────────────────────────────────────────────────────────
    builder.Host.UseSerilog((ctx, _, cfg) =>
        cfg.ReadFrom.Configuration(ctx.Configuration)
           .WriteTo.Console());

    // ── Core services ─────────────────────────────────────────────────────────
    builder.Services.AddCommonServices(builder.Configuration);
    builder.Services.AddDataLayer();

    // ── Business layer ────────────────────────────────────────────────────────
    builder.Services.AddScoped<IMetricsService, MetricsService>();

    // ── Caching ───────────────────────────────────────────────────────────────
    builder.Services.AddMemoryCache(opts => opts.SizeLimit = 100);

    // ── Controllers & Swagger ─────────────────────────────────────────────────
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new() { Title = "LLM Shadow Compare Service", Version = "v1" });
    });

    // ── Health checks ─────────────────────────────────────────────────────────
    builder.Services.AddHealthChecks()
        .AddDbContextCheck<LlmShadow.DataLayer.ShadowDbContext>("database");

    // ── Response compression ──────────────────────────────────────────────────
    builder.Services.AddResponseCompression(opts => opts.EnableForHttps = true);

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
    app.UseResponseCompression();

    app.MapControllers();
    app.MapHealthChecks("/health");

    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "LlmShadow.CompareService terminated unexpectedly");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}
