// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application

using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;

using Logibooks.Core.Models;

namespace Logibooks.Core.Tests.Services;

[TestFixture]
public class WordsLookupContextTests
{
    private static StopWord swSymbols1 = new() { Id = 575, Word = "575", MatchTypeId = (int)WordMatchTypeCode.ExactSymbols };
    private static StopWord swSymbols2 = new() { Id = 900, Word = "900", MatchTypeId = (int)WordMatchTypeCode.ExactSymbols };
    private static StopWord swWord1 = new() { Id = 1, Word = "золото", MatchTypeId = (int)WordMatchTypeCode.ExactWord };
    private static StopWord swWord2 = new() { Id = 2, Word = "чек", MatchTypeId = (int)WordMatchTypeCode.ExactWord };
    private static StopWord swWord3 = new() { Id = 3, Word = "квадрокоптер", MatchTypeId = (int)WordMatchTypeCode.ExactWord };
    private static StopWord swPhrase1 = new() { Id = 4, Word = "patek philippе", MatchTypeId = (int)WordMatchTypeCode.Phrase };
    private static StopWord swPhrase2 = new() { Id = 5, Word = "часы премиальные", MatchTypeId = (int)WordMatchTypeCode.Phrase };

    private static List<StopWord> AllStopWords => new() { swSymbols1, swSymbols2, swWord1, swWord2, swWord3, swPhrase1, swPhrase2 };

    private static WordsLookupContext<StopWord> CreateContext() =>
        new WordsLookupContext<StopWord>(AllStopWords);

    private static List<StopWord> Match(string productName)
    {
        var context = CreateContext();
        return context.GetMatchingWords(productName).ToList();
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
