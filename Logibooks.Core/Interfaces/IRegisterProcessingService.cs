// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application

using Logibooks.Core.RestModels;

namespace Logibooks.Core.Interfaces;

public interface IRegisterProcessingService
{
    protected const int OzonId = 1;
    protected const int WBRId = 2;

    static public int GetOzonId() => OzonId;
    static public int GetWBRId() => WBRId;
    Task<Reference> UploadRegisterFromExcelAsync(
        int companyId,
        byte[] content,
        string fileName,
        CancellationToken cancellationToken = default);

    Task<byte[]> DownloadRegisterToExcelAsync(
        int registerId,
        CancellationToken cancellationToken = default);
}
