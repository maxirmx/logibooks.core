// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application

namespace Logibooks.Core.Interfaces;

public interface IFeacnListProcessingService
{
    /// <summary>
    /// Uploads FEACN codes from Excel file and replaces existing data.
    /// </summary>
    /// <param name="content">Excel file content as byte array</param>
    /// <param name="fileName">Original file name for logging purposes</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task UploadFeacnCodesAsync(
        byte[] content,
        string fileName,
        CancellationToken cancellationToken = default);
}