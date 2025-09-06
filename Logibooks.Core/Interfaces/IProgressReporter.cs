// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application

namespace Logibooks.Core.Interfaces;

using Logibooks.Core.RestModels;

public interface IProgressReporter
{
    ValidationProgress? GetProgress(Guid handleId);
    bool Cancel(Guid handleId);
}

