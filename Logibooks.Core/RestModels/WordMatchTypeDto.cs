// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application

namespace Logibooks.Core.RestModels;

using Logibooks.Core.Models;

public class WordMatchTypeDto(WordMatchType matchType)
{
    public int Id { get; set; } = matchType.Id;
    public string Name { get; set; } = matchType.Name;
}
