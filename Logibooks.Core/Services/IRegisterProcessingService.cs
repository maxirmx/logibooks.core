using Logibooks.Core.RestModels;

namespace Logibooks.Core.Services;

public interface IRegisterProcessingService
{
    Task<Reference> UploadWbrRegisterFromExcelAsync(
        int companyId,
        byte[] content,
        string fileName,
        string mappingFile = "wbr_register_mapping.yaml",
        CancellationToken cancellationToken = default);
}
