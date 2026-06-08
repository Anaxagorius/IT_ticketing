using ITTicketing.Api.Services;
using Microsoft.Extensions.Options;

namespace ITTicketing.Api.Services;

public sealed class SlaMonitoringService(
    IServiceScopeFactory scopeFactory,
    IOptions<SlaMonitoringOptions> options,
    ILogger<SlaMonitoringService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalSeconds = Math.Max(30, options.Value.IntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await EvaluateAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "SLA monitoring iteration failed");
            }

            await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken);
        }
    }

    private async Task EvaluateAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var evaluator = scope.ServiceProvider.GetRequiredService<SlaEvaluator>();
        await evaluator.EvaluateAsync(cancellationToken);
    }
}

