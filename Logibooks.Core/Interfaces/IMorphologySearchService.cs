// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application

using Logibooks.Core.Models;

namespace Logibooks.Core.Interfaces;

public enum MorphologySupportLevel
{
    NoSupport,
    FormsSupport,
    FullSupport
}



public interface IMorphologySearchService
{
    MorphologyContext InitializeContext(IEnumerable<StopWord> stopWords);
    IEnumerable<int> CheckText(MorphologyContext context, string text);
    MorphologySupportLevel CheckWord(string word);
}

public class MorphologyContext
{
    internal Dictionary<string, HashSet<int>> NormalForms { get; } = new();
    internal Dictionary<Pullenti.Semantic.Utils.DerivateGroup, HashSet<int>> Groups { get; } = new();
}
