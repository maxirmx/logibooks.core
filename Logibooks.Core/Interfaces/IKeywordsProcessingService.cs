// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application

using Logibooks.Core.Models;

namespace Logibooks.Core.Interfaces;

public interface IKeywordsProcessingService
{
    Task<List<KeyWord>> UploadKeywordsFromExcelAsync(
        byte[] content,
        string fileName,
        CancellationToken cancellationToken = default);
}

