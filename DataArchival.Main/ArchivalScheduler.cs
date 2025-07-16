using DataArchival.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DataArchival.Main;

public class ArchivalScheduler : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ArchivalScheduler> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromHours(24);

    public ArchivalScheduler(IServiceProvider serviceProvider,
        ILogger<ArchivalScheduler> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Archival Scheduler Service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var archivalService = scope.ServiceProvider.GetRequiredService<IDataArchivalService>();

                _logger.LogInformation("Starting scheduled archival process");
                var logs = await archivalService.ArchiveAllConfiguredTablesAsync();
                _logger.LogInformation("Completed scheduled archival process. Processed {Count} tables", logs.Count);

                await Task.Delay(_interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Archival Scheduler Service cancelled");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in scheduled archival process");
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken); // Wait 5 minutes before retrying
            }
        }

        _logger.LogInformation("Archival Scheduler Service stopped");
    }
}