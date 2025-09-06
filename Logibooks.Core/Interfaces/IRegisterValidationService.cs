// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application

using Logibooks.Core.RestModels;

namespace Logibooks.Core.Interfaces;

public interface IRegisterValidationService : IProgressReporter
{
    Task<Guid> StartSwValidationAsync(int registerId, CancellationToken cancellationToken = default);
    Task<Guid> StartFeacnValidationAsync(int registerId, CancellationToken cancellationToken = default);
}
