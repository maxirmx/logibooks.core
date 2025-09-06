// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application

namespace Logibooks.Core.Models;

public enum WordMatchTypeCode
{
    ExactSymbols = 1,
    ExactWord = 11,
    Phrase = 21,
    MorphologyMatchTypes = 41,
#pragma warning disable CA1069 // Enums values should not be duplicated
    WeakMorphology = 41,
#pragma warning restore CA1069 // Enums values should not be duplicated
    StrongMorphology = 51
}
