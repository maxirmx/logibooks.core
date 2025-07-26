using Logibooks.Core.Models;

namespace Logibooks.Core.Services;

public interface IOrderIndPostGenerator
{
    Task<string> GenerateXML(int orderId);
    Task<string> GenerateXML(BaseOrder order);
    Task<string> GenerateFilename(int orderId);
    string GenerateFilename(BaseOrder order);
}
