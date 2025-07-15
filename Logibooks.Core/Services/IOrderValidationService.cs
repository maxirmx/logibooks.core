using System.Threading;
using System.Threading.Tasks;

namespace Logibooks.Core.Services;

public interface IOrderValidationService
{
    Task ValidateAsync(int orderId, CancellationToken cancellationToken = default);
}
