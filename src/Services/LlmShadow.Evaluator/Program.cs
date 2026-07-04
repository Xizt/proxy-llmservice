using LlmShadow.Common.Extensions;
using LlmShadow.DataLayer.Common;
using LlmShadow.DataLayer.Extensions;
using LlmShadow.Evaluation.Extensions;
using LlmShadow.Evaluator.BusinessLayer;
using LlmShadow.Evaluator.Workers;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting LlmShadow.Evaluator");

    var host = Host.CreateDefaultBuilder(args)
        .UseSerilog((ctx, _, cfg) =>
            cfg.ReadFrom.Configuration(ctx.Configuration)
               .WriteTo.Console())
        .ConfigureServices((ctx, services) =>
        {
            services.AddCommonServices(ctx.Configuration);
            services.AddDataLayer();
            services.AddEvaluation();

            services.AddScoped<IEvaluationJobService, EvaluationJobService>();
            services.AddHostedService<EvaluatorWorker>();
        })
        .Build();

    using (var scope = host.Services.CreateScope())
    {
        var migrator = scope.ServiceProvider.GetRequiredService<DatabaseMigrator>();
        await migrator.MigrateAsync();
    }

    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "LlmShadow.Evaluator terminated unexpectedly");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}
