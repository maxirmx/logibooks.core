// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application

namespace Logibooks.Core.RestModels;

using Logibooks.Core.Models;

public class StopWordDto
{
    public int Id { get; set; }
    public string Word { get; set; } = string.Empty;
    public int MatchTypeId { get; set; }

    public StopWordDto() {}
    public StopWordDto(StopWord sw)
    {
        Id = sw.Id;
        Word = sw.Word;
        MatchTypeId = (int)sw.MatchTypeId;
    }

    public StopWord ToModel()
    {
        return new StopWord
        {
            Id = Id,
            Word = Word,
            MatchTypeId = MatchTypeId
        };
    }
}
