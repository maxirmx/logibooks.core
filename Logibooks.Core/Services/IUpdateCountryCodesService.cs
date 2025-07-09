namespace Logibooks.Core.Services;

public interface IUpdateCountryCodesService
{
    Task RunAsync(CancellationToken cancellationToken = default);
}
