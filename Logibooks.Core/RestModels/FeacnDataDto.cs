namespace Logibooks.Core.RestModels;

public class FeacnDataDto
{
    public List<FeacnOrderDto> Orders { get; set; } = [];
    public List<FeacnPrefixDto> Prefixes { get; set; } = [];
    public List<FeacnPrefixExceptionDto> Exceptions { get; set; } = [];
}
