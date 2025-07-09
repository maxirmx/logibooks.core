using Microsoft.Extensions.Logging;

namespace Logibooks.Core.Services;

public class UpdateCountryCodesService(ILogger<UpdateCountryCodesService> logger)
{
    private readonly ILogger<UpdateCountryCodesService> _logger = logger;

    public Task RunAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("UpdateCountryCodesService stub running");
        return Task.CompletedTask;
    }
}
