// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application

using System.ComponentModel.DataAnnotations;

namespace Logibooks.Core.RestModels;

using Logibooks.Core.Models;

public class KeyWordDto
{
    public int Id { get; set; }
    
    public string Word { get; set; } = string.Empty;
    
    public int MatchTypeId { get; set; }
    
    public List<string> FeacnCodes { get; set; } = [];

    public KeyWordDto() { }

    public KeyWordDto(KeyWord kw)
    {
        Id = kw.Id;
        Word = kw.Word;
        MatchTypeId = kw.MatchTypeId;
        FeacnCodes = kw.KeyWordFeacnCodes?.Select(kwfc => kwfc.FeacnCode).ToList() ?? [];
    }

    public KeyWord ToModel()
    {
        var keyWord = new KeyWord
        {
            Id = Id,
            Word = Word,
            MatchTypeId = MatchTypeId
        };

        keyWord.KeyWordFeacnCodes = [.. FeacnCodes.Select(fc => new KeyWordFeacnCode
        {
            KeyWordId = Id,
            FeacnCode = fc,
            KeyWord = keyWord
        })];

        return keyWord;
    }
}
