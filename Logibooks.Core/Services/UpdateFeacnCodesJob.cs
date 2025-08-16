using Logibooks.Core.Interfaces;
using Quartz;

namespace Logibooks.Core.Services;

public class UpdateFeacnCodesJob(IUpdateFeacnCodesService service, ILogger<UpdateFeacnCodesJob> logger) : IJob
{
    private readonly IUpdateFeacnCodesService _service = service;
    private readonly ILogger<UpdateFeacnCodesJob> _logger = logger;

    private static CancellationTokenSource? _prev;
    private static readonly object _lock = new();

    public async Task Execute(IJobExecutionContext context)
    {
        CancellationTokenSource cts;
        lock (_lock)
        {
            _prev?.Cancel();
            cts = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken);
            _prev = cts;
        }

        _logger.LogInformation("Executing UpdateFeacnCodesJob");
        try
        {
            await _service.RunAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("UpdateFeacnCodesJob was cancelled");
        }
        finally
        {
            cts.Dispose();
            lock (_lock)
            {
                if (_prev == cts) _prev = null;
            }
        }
    }
}

