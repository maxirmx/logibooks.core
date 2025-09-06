// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application

using Logibooks.Core.Models;
using System.Runtime.CompilerServices;
[assembly: InternalsVisibleTo("Logibooks.Core.Tests")]

namespace Logibooks.Core.Interfaces;

public interface IParcelFeacnCodeLookupService
{
    Task<List<int>> LookupAsync(BaseParcel order,
        MorphologyContext morphologyContext,
        WordsLookupContext<KeyWord> wordsLookupContext,
        CancellationToken cancellationToken = default);
}
