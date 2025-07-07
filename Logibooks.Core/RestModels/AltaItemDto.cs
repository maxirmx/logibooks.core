namespace Logibooks.Core.RestModels;

using Logibooks.Core.Models;

public class AltaItemDto : AltaBaseDto<AltaItem>
{
    public AltaItemDto() {}
    public AltaItemDto(AltaItem item) : base(item) {}
}
