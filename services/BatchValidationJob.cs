/*
 * File: BatchValidationJob.cs
 * Description: Periodic background task executing case validation matches.
 * To Implement: Keep logging descriptive.
 */

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ccms_backend.services;

public class BatchValidationJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<BatchValidationJob> _logger;

    public BatchValidationJob(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<BatchValidationJob> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalMinutesStr = _configuration["BatchJob:IntervalMinutes"] ?? "15";
        if (!double.TryParse(intervalMinutesStr, out double intervalMinutes))
        {
            intervalMinutes = 15;
        }

        _logger.LogInformation("BatchValidationJob started with an interval of {Interval} minutes.", intervalMinutes);

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(intervalMinutes));

        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            _logger.LogInformation("Executing background batch validation tick...");

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var batchValidationService = scope.ServiceProvider.GetRequiredService<BatchValidationService>();

                var result = await batchValidationService.RunBatchValidationAsync(isManualTrigger: false, stoppingToken);

                _logger.LogInformation(
                    "Background validation run completed: Processed={Processed}, Validated={Validated}, NotFound={NotFound} in {Duration}ms.",
                    result.CasesProcessed, result.CasesValidated, result.CasesNotFound, result.DurationMs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred executing background batch validation job.");
            }
        }

        _logger.LogInformation("BatchValidationJob is stopping.");
    }
}
// Note: Handled by HostedService runtime container on startup.
