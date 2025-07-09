using Microsoft.Extensions.Logging;
using Quartz;

namespace Logibooks.Core.Services;

public class UpdateCountryCodesJob(UpdateCountryCodesService service, ILogger<UpdateCountryCodesJob> logger) : IJob
{
    private readonly UpdateCountryCodesService _service = service;
    private readonly ILogger<UpdateCountryCodesJob> _logger = logger;

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation("Executing UpdateCountryCodesJob");
        await _service.RunAsync(context.CancellationToken);
    }
}
