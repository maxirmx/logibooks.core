// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application

using Logibooks.Core.Data;
using Logibooks.Core.Models;
using System.Runtime.CompilerServices;
[assembly: InternalsVisibleTo("Logibooks.Core.Tests")]

namespace Logibooks.Core.Interfaces;

public interface IParcelValidationService
{
    Task ValidateKwAsync(
        BaseParcel order,
        MorphologyContext morphologyContext,
        WordsLookupContext<StopWord> wordsLookupContext,
        CancellationToken cancellationToken = default);

    Task ValidateFeacnAsync(
        BaseParcel order,
        FeacnPrefixCheckContext? feacnContext = null,
        CancellationToken cancellationToken = default);

    // Overloaded methods that accept DbContext for thread-safe operations
    Task ValidateKwAsync(
        AppDbContext dbContext,
        BaseParcel parcel,
        MorphologyContext morphologyContext,
        WordsLookupContext<StopWord> wordsLookupContext,
        CancellationToken cancellationToken = default);

    Task ValidateFeacnAsync(
        AppDbContext dbContext,
        BaseParcel parcel,
        FeacnPrefixCheckContext? feacnContext = null,
        CancellationToken cancellationToken = default);
}
