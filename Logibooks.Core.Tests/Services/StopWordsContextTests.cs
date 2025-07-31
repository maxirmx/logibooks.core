// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application
//
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions
// are met:
// 1. Redistributions of source code must retain the above copyright
// notice, this list of conditions and the following disclaimer.
// 2. Redistributions in binary form must reproduce the above copyright
// notice, this list of conditions and the following disclaimer in the
// documentation and/or other materials provided with the distribution.
//
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// 'AS IS' AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED
// TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR
// PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDERS OR CONTRIBUTORS
// BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
// CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
// SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
// INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
// CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
// ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
// POSSIBILITY OF SUCH DAMAGE.

using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;

using Logibooks.Core.Models;
using Logibooks.Core.Services;

namespace Logibooks.Core.Tests.Services;

[TestFixture]
public class StopWordsContextTests
{
    private static StopWord swSymbols1 = new() { Id = 575, Word = "575", MatchTypeId = (int)StopWordMatchTypeCode.ExactSymbols };
    private static StopWord swSymbols2 = new() { Id = 900, Word = "900", MatchTypeId = (int)StopWordMatchTypeCode.ExactSymbols };
    private static StopWord swWord1 = new() { Id = 1, Word = "золото", MatchTypeId = (int)StopWordMatchTypeCode.ExactWord };
    private static StopWord swWord2 = new() { Id = 2, Word = "чек", MatchTypeId = (int)StopWordMatchTypeCode.ExactWord };
    private static StopWord swWord3 = new() { Id = 3, Word = "квадрокоптер", MatchTypeId = (int)StopWordMatchTypeCode.ExactWord };
    private static StopWord swPhrase1 = new() { Id = 4, Word = "patek philippе", MatchTypeId = (int)StopWordMatchTypeCode.Phrase };
    private static StopWord swPhrase2 = new() { Id = 5, Word = "часы премиальные", MatchTypeId = (int)StopWordMatchTypeCode.Phrase };

    private static List<StopWord> AllStopWords => new() { swSymbols1, swSymbols2, swWord1, swWord2, swWord3, swPhrase1, swPhrase2 };

    private static StopWordsContext CreateContext() =>
        new OrderValidationService(null!, null!, null!).InitializeStopWordsContext(AllStopWords);

    private static List<StopWord> Match(string productName)
    {
        var context = CreateContext();
        var method = typeof(OrderValidationService)
            .GetMethod("GetMatchingStopWordsFromContext", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var temp = method?.Invoke(null, [productName, context]);
        return temp is not null ? (List<StopWord>)temp : [];
    }

    [Test]
    public void ExactSymbolsMatch_FindsCorrectStopWords()
    {
        var result = Match("This contains 575 and also 900");
        Assert.That(result.Any(sw => sw.Id == 575), "Should match 575");
        Assert.That(result.Any(sw => sw.Id == 900), "Should match 900");
    }

    [Test]
    public void ExactWordMatch_FindsCorrectStopWords()
    {
        var result = Match("Квадрокоптер, золото и чек");
        Assert.That(result.Any(sw => sw.Id == 1), "Should match золото");
        Assert.That(result.Any(sw => sw.Id == 2), "Should match чек");
        Assert.That(result.Any(sw => sw.Id == 3), "Should match квадрокоптер");
    }

    [Test]
    public void PhraseMatch_FindsCorrectStopWords()
    {
        var result = Match("часы премиальные для patek philippе коллекции");
        Assert.That(result.Any(sw => sw.Id == 4), "Should match patek philippе");
        Assert.That(result.Any(sw => sw.Id == 5), "Should match часы премиальные");
    }

    [Test]
    public void PhraseMatch_DoesNotMatchIfWordsAreNotInOrder()
    {
        var result = Match("премиальные часы коллекция");
        Assert.That(result.All(sw => sw.Id != 5), "Should not match 'часы премиальные' if not in order");
    }

    [Test]
    public void PhraseMatch_DoesNotMatchIfWordsAreSeparatedByOtherWords()
    {
        var result = Match("часы очень премиальные");
        Assert.That(result.All(sw => sw.Id != 5), "Should not match 'часы премиальные' if separated by other words");
    }

    [Test]
    public void ExactWordMatch_IgnoresCaseAndDelimiters()
    {
        var result = Match("ЗОЛОТО, чек! квадрокоптер?");
        Assert.That(result.Any(sw => sw.Id == 1), "Should match золото case-insensitively");
        Assert.That(result.Any(sw => sw.Id == 2), "Should match чек case-insensitively");
        Assert.That(result.Any(sw => sw.Id == 3), "Should match квадрокоптер case-insensitively");
    }

    [Test]
    public void PhraseMatch_IgnoresCaseAndDelimiters()
    {
        var result = Match("PATEK PHILIPPЕ и ЧАСЫ ПРЕМИАЛЬНЫЕ");
        Assert.That(result.Any(sw => sw.Id == 4), "Should match patek philippе case-insensitively");
        Assert.That(result.Any(sw => sw.Id == 5), "Should match часы премиальные case-insensitively");
    }
}
