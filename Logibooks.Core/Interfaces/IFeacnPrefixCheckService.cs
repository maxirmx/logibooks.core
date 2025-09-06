// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application

using Logibooks.Core.Models;

namespace Logibooks.Core.Interfaces;

public interface IFeacnPrefixCheckService
{
    Task<IEnumerable<BaseParcelFeacnPrefix>> CheckParcelAsync(BaseParcel parcel, CancellationToken cancellationToken = default);
    IEnumerable<BaseParcelFeacnPrefix> CheckParcel(BaseParcel parcel, FeacnPrefixCheckContext context);
    Task<FeacnPrefixCheckContext> CreateContext(CancellationToken cancellationToken = default);
}

public class FeacnPrefixCheckContext
{
    internal Dictionary<string, List<FeacnPrefix>> Prefixes { get; } = new();
}
