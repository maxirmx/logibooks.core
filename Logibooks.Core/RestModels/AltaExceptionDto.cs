namespace Logibooks.Core.RestModels;

using Logibooks.Core.Models;

public class AltaExceptionDto : AltaBaseDto<AltaException>
{
    public AltaExceptionDto() {}
    public AltaExceptionDto(AltaException item) : base(item) {}
}
