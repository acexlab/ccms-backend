using CCMS.Application.Interfaces;
using CCMS.Application.Services;
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using CCMS.Domain.Entities;

namespace CCMS.Infrastructure.Services;

public class BatchSchedulerService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<BatchSchedulerService> _logger;
    private readonly IConfiguration _configuration;

    public BatchSchedulerService(
        IServiceProvider serviceProvider, 
        ILogger<BatchSchedulerService> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Read scheduled interval from configuration. Default to 15 minutes.
        int intervalMinutes = _configuration.GetValue<int>("BatchSettings:IntervalMinutes", 15);
        var period = TimeSpan.FromMinutes(intervalMinutes);

        _logger.LogInformation("BatchSchedulerService started with run interval: {Period}.", period);

        // Run immediately at startup
        try
        {
            _logger.LogInformation("Triggering initial startup scheduled batch run...");
            await RunScheduledJobAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing initial startup scheduled batch job.");
        }

        using PeriodicTimer timer = new PeriodicTimer(period);

        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                _logger.LogInformation("Triggering scheduled batch run...");
                await RunScheduledJobAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing scheduled batch job.");
            }
        }
    }

    private async Task RunScheduledJobAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var batchValidationService = scope.ServiceProvider.GetRequiredService<BatchValidationService>();
        
        // Execute the batch job using Scheduled trigger
        var log = await batchValidationService.TriggerBatchRunAsync(TriggeredBy.Scheduled, userId: null);
        _logger.LogInformation("Scheduled batch run {RunId} completed successfully. Cases processed: {ProcessedCount}, Matched: {MatchedCount}, Not Found: {NotFoundCount}.", 
            log.RunId, log.CasesProcessed, log.AccountsMatched, log.AccountsNotFound);
    }
}
