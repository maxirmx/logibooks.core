using Logibooks.Core.Models;

namespace Logibooks.Core.Services;

public interface IOrderIndPostGenerator
{
    Task<(string, string)> GenerateXML(int orderId);
    Task<(string, byte[])> GenerateXML4R(int registerId);
}
