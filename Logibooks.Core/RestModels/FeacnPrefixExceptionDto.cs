using Logibooks.Core.Models;

namespace Logibooks.Core.RestModels;

public class FeacnPrefixExceptionDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public int FeacnPrefixId { get; set; }

    public FeacnPrefixExceptionDto() { }
    public FeacnPrefixExceptionDto(FEACNPrefixException e)
    {
        Id = e.Id;
        Code = e.Code;
        FeacnPrefixId = e.FeacnPrefixId;
    }
}
