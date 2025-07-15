using Logibooks.Core.RestModels;

namespace Logibooks.Core.Services;

public interface IRegisterValidationService
{
    Task<Guid> StartValidationAsync(int registerId, CancellationToken cancellationToken = default);
    ValidationProgress? GetProgress(Guid handleId);
    bool CancelValidation(Guid handleId);
}
