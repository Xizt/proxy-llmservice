using LlmShadow.Common.Extensions;
using LlmShadow.DataLayer.Common;
using LlmShadow.DataLayer.Extensions;
using LlmShadow.Inference.Extensions;
using LlmShadow.Messaging.Extensions;
using LlmShadow.SecondaryProcessor.BusinessLayer;
using LlmShadow.SecondaryProcessor.Workers;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting LlmShadow.SecondaryProcessor");

    var host = Host.CreateDefaultBuilder(args)
        .UseSerilog((ctx, _, cfg) =>
            cfg.ReadFrom.Configuration(ctx.Configuration)
               .WriteTo.Console())
        .ConfigureServices((ctx, services) =>
        {
            services.AddCommonServices(ctx.Configuration);
            services.AddDataLayer();
            services.AddInferenceClient();
            services.AddMessaging();

            services.AddScoped<IShadowExecutionService, ShadowExecutionService>();
            services.AddHostedService<ShadowProcessorWorker>();
        })
        .Build();

    // Apply DB migrations before starting the worker loop.
    using (var scope = host.Services.CreateScope())
    {
        var migrator = scope.ServiceProvider.GetRequiredService<DatabaseMigrator>();
        await migrator.MigrateAsync();
    }

    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "LlmShadow.SecondaryProcessor terminated unexpectedly");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}
