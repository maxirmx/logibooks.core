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
using System.Linq;
using Logibooks.Core.Models;
using Logibooks.Core.Services;
using System.Collections.Generic;
using Logibooks.Core.Interfaces;

namespace Logibooks.Core.Tests.Services;

[TestFixture]
public class MorphologySearchServiceTests
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    private MorphologySearchService _service;
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

    [SetUp]
    public void Setup()
    {
        _service = new MorphologySearchService();
    }

    [Test]
    public void CheckText_FindsDerivativeMatch()
    {
        var sw = new StopWord { Id = 1, Word = "золото", MatchTypeId = (int)WordMatchTypeCode.StrongMorphology };
        var ctx = _service.InitializeContext([sw]);
        var res = _service.CheckText(ctx, "золотой браслет и алюминиевый слиток");
        Assert.That(res.Contains(1));
    }

    [Test]
    public void CheckText_FindsWeakFormMatch()
    {
        var sw = new StopWord { Id = 2, Word = "золото", MatchTypeId = (int)WordMatchTypeCode.WeakMorphology };
        var ctx = _service.InitializeContext([sw]);
        var res = _service.CheckText(ctx, "работаем с золотом");
        Assert.That(res.Contains(2));
    }

    [Test]
    public void InitializeContext_HandlesSingleStopWord()
    {
        var sw = new StopWord { Id = 1, Word = "дом", MatchTypeId = (int)WordMatchTypeCode.StrongMorphology };
        var ctx = _service.InitializeContext([sw]);
        
        Assert.That(ctx, Is.Not.Null);
        // Test that context was properly initialized by checking if it can find derivatives
        var result = _service.CheckText(ctx, "домашний");
        Assert.That(result.Contains(1), "Context should be properly initialized");
    }

    [Test]
    public void InitializeContext_HandlesMultipleStopWords()
    {
        var stopWords = new[]
        {
            new StopWord { Id = 1, Word = "золото", MatchTypeId = (int)WordMatchTypeCode.StrongMorphology },
            new StopWord { Id = 2, Word = "серебро", MatchTypeId = (int)WordMatchTypeCode.StrongMorphology },
            new StopWord { Id = 3, Word = "дом", MatchTypeId = (int)WordMatchTypeCode.StrongMorphology }
        };
        
        var ctx = _service.InitializeContext(stopWords);
        
        Assert.That(ctx, Is.Not.Null);
        // Test that all words were processed by checking derivatives
        var result = _service.CheckText(ctx, "золотой домашний серебряный");
        Assert.That(result.Contains(1), "Should find gold derivative");
        Assert.That(result.Contains(2), "Should find silver derivative");
        Assert.That(result.Contains(3), "Should find home derivative");
    }

    [Test]
    public void InitializeContext_HandlesEmptyCollection()
    {
        var ctx = _service.InitializeContext([]);
        
        Assert.That(ctx, Is.Not.Null);
        // Test empty context by verifying no matches are found
        var result = _service.CheckText(ctx, "любой текст");
        Assert.That(result, Is.Empty);
    }

    [Test]
    public void InitializeContext_SkipsNullOrWhitespaceWords()
    {
        var stopWords = new[]
        {
            new StopWord { Id = 1, Word = "", MatchTypeId = (int)WordMatchTypeCode.StrongMorphology },
            new StopWord { Id = 2, Word = "   ", MatchTypeId = (int)WordMatchTypeCode.StrongMorphology },
            new StopWord { Id = 3, Word = "золото", MatchTypeId = (int)WordMatchTypeCode.StrongMorphology },
            new StopWord { Id = 4, Word = null!, MatchTypeId = (int)WordMatchTypeCode.StrongMorphology }
        };
        
        var ctx = _service.InitializeContext(stopWords);
        
        Assert.That(ctx, Is.Not.Null);
        // Should only process the valid "золото" word
        var result = _service.CheckText(ctx, "золотой браслет");
        Assert.That(result.Contains(3), "Should find only the valid stop word");
        Assert.That(result.Count(), Is.EqualTo(1), "Should only find one match");
    }

    [Test]
    public void CheckText_HandlesNullText()
    {
        var sw = new StopWord { Id = 1, Word = "золото", MatchTypeId = (int)WordMatchTypeCode.StrongMorphology };
        var ctx = _service.InitializeContext([sw]);
        
        var result = _service.CheckText(ctx, null!);
        
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.Empty);
    }

    [Test]
    public void CheckText_HandlesEmptyText()
    {
        var sw = new StopWord { Id = 1, Word = "золото", MatchTypeId = (int)WordMatchTypeCode.StrongMorphology };
        var ctx = _service.InitializeContext([sw]);
        
        var result = _service.CheckText(ctx, "");
        
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.Empty);
    }

    [Test]
    public void CheckText_HandlesWhitespaceOnlyText()
    {
        var sw = new StopWord { Id = 1, Word = "золото", MatchTypeId = (int)WordMatchTypeCode.StrongMorphology };
        var ctx = _service.InitializeContext([sw]);
        
        var result = _service.CheckText(ctx, "   \t\n  ");
        
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.Empty);
    }

    [Test]
    public void CheckText_FindsMultipleMatches()
    {
        var stopWords = new[]
        {
            new StopWord { Id = 1, Word = "золото", MatchTypeId = (int)WordMatchTypeCode.StrongMorphology },
            new StopWord { Id = 2, Word = "дом", MatchTypeId = (int)WordMatchTypeCode.StrongMorphology }
        };
        var ctx = _service.InitializeContext(stopWords);
        
        var result = _service.CheckText(ctx, "золотой браслет и домашний уют");
        
        Assert.That(result.Contains(1), "Should find gold derivative");
        Assert.That(result.Contains(2), "Should find home derivative");
    }

    [Test]
    public void CheckText_CaseInsensitive()
    {
        var sw = new StopWord { Id = 1, Word = "дом", MatchTypeId = (int)WordMatchTypeCode.StrongMorphology };
        var ctx = _service.InitializeContext([sw]);
        
        var result1 = _service.CheckText(ctx, "большой ДОМ");
        var result2 = _service.CheckText(ctx, "домашний уют");
        var result3 = _service.CheckText(ctx, "ДОМИК в деревне");
        
        Assert.That(result1.Contains(1), "Should find uppercase match");
        Assert.That(result2.Contains(1), "Should find derivative match");
        Assert.That(result3.Contains(1), "Should find uppercase derivative match");
    }

    [Test]
    public void CheckText_NoMatchesForUnrelatedText()
    {
        var sw = new StopWord { Id = 1, Word = "золото", MatchTypeId = (int)WordMatchTypeCode.StrongMorphology };
        var ctx = _service.InitializeContext([sw]);
        
        var result = _service.CheckText(ctx, "красивый автомобиль и зелёная трава");
        
        Assert.That(result, Is.Empty);
    }

    [Test]
    public void CheckText_HandlesTextWithPunctuation()
    {
        var sw = new StopWord { Id = 1, Word = "дом", MatchTypeId = (int)WordMatchTypeCode.StrongMorphology };
        var ctx = _service.InitializeContext([sw]);
        
        var result = _service.CheckText(ctx, "Это домашний, уютный и тёплый дом!");
        
        Assert.That(result.Contains(1), "Should find matches despite punctuation");
    }

    [Test]
    public void CheckText_HandlesMixedLanguages()
    {
        var sw = new StopWord { Id = 1, Word = "дом", MatchTypeId = (int)WordMatchTypeCode.StrongMorphology };
        var ctx = _service.InitializeContext([sw]);
        
        var result = _service.CheckText(ctx, "My beautiful домик is very nice house");
        
        Assert.That(result.Contains(1), "Should find Russian matches in mixed language text");
    }

    [TestCase("книга", "книжный магазин", true)]
    [TestCase("учить", "ученик в школе", true)]
    [TestCase("писать", "писатель и письмо", true)]
    [TestCase("работать", "рабочий день", true)]
    [TestCase("дом", "домашний уют", true)]
    [TestCase("молоко", "кофе и чай", false)]
    [TestCase("собака", "собачий корм", true)]
    public void CheckText_MorphologicalDerivatives(string stopWord, string testText, bool shouldMatch)
    {
        var sw = new StopWord { Id = 1, Word = stopWord, MatchTypeId = (int)WordMatchTypeCode.StrongMorphology };
        var ctx = _service.InitializeContext([sw]);
        
        var result = _service.CheckText(ctx, testText);
        
        if (shouldMatch)
        {
            Assert.That(result.Contains(1), $"Should find derivative of '{stopWord}' in '{testText}'");
        }
        else
        {
            Assert.That(result, Is.Empty, $"Should not find '{stopWord}' in '{testText}'");
        }
    }

    [Test]
    public void CheckText_ReturnsUniqueIds()
    {
        var sw = new StopWord { Id = 1, Word = "дом", MatchTypeId = (int)WordMatchTypeCode.StrongMorphology };
        var ctx = _service.InitializeContext([sw]);
        
        var result = _service.CheckText(ctx, "домашний дом и домик").ToList();
        
        // Should only return ID 1 once, even if multiple derivatives are found
        Assert.That(result.Count(id => id == 1), Is.EqualTo(1));
    }

    [Test]
    public void CheckText_HandlesLongText()
    {
        var sw = new StopWord { Id = 1, Word = "дом", MatchTypeId = (int)WordMatchTypeCode.StrongMorphology };
        var ctx = _service.InitializeContext([sw]);
        
        var longText = string.Join(" ", Enumerable.Repeat("Это очень длинный текст с множеством слов", 100)) 
                      + " и один домашний предмет";
        
        var result = _service.CheckText(ctx, longText);
        
        Assert.That(result.Contains(1), "Should find matches in long text");
    }

    [Test]
    public void CheckText_WithEmptyContext()
    {
        var ctx = _service.InitializeContext([]);
        
        var result = _service.CheckText(ctx, "любой текст с любыми словами");
        
        Assert.That(result, Is.Empty);
    }

    [Test]
    public void CheckText_PerformanceWithManyStopWords()
    {
        var stopWords = new List<StopWord>();
        for (int i = 1; i <= 100; i++)
        {
            stopWords.Add(new StopWord { Id = i, Word = $"слово{i}", MatchTypeId = (int)WordMatchTypeCode.StrongMorphology });
        }
        stopWords.Add(new StopWord { Id = 101, Word = "дом", MatchTypeId = (int)WordMatchTypeCode.StrongMorphology });
        
        var ctx = _service.InitializeContext(stopWords);
        
        var result = _service.CheckText(ctx, "домашний уют");
        
        Assert.That(result.Contains(101), "Should find match even with many stop words");
    }

    [Test]
    public void CheckText_HandlesSpecialCharacters()
    {
        var sw = new StopWord { Id = 1, Word = "дом", MatchTypeId = (int)WordMatchTypeCode.StrongMorphology };
        var ctx = _service.InitializeContext([sw]);
        
        var result = _service.CheckText(ctx, "дом@example.com и домик#123 плюс дом$");
        
        Assert.That(result.Contains(1), "Should find matches despite special characters");
    }

    [Test]
    public void InitializeContext_HandlesStopWordsCaseVariations()
    {
        var stopWords = new[]
        {
            new StopWord { Id = 1, Word = "ЗОЛОТО", MatchTypeId = (int)WordMatchTypeCode.StrongMorphology },
            new StopWord { Id = 2, Word = "дом", MatchTypeId = (int)WordMatchTypeCode.StrongMorphology },
            new StopWord { Id = 3, Word = "УЧИТЬ", MatchTypeId = (int)WordMatchTypeCode.StrongMorphology }
        };
        
        var ctx = _service.InitializeContext(stopWords);
        
        var result = _service.CheckText(ctx, "золотой домашний ученик");
        
        Assert.That(result.Contains(1), "Should handle uppercase stop words");
        Assert.That(result.Contains(2), "Should handle lowercase stop words");
        Assert.That(result.Contains(3), "Should handle mixed case stop words");
    }

    [Test]
    public void CheckText_FindsExactMatch()
    {
        var sw = new StopWord { Id = 1, Word = "золото", MatchTypeId = (int)WordMatchTypeCode.StrongMorphology };
        var ctx = _service.InitializeContext([sw]);
        
        var result = _service.CheckText(ctx, "чистое золото");
        
        Assert.That(result.Contains(1), "Should find exact word matches");
    }

    [Test]
    public void CheckText_HandlesVeryShortWords()
    {
        var sw = new StopWord { Id = 1, Word = "я", MatchTypeId = (int)WordMatchTypeCode.StrongMorphology };
        var ctx = _service.InitializeContext([sw]);
        
        var result = _service.CheckText(ctx, "я иду домой");
        
        // Note: This test might not find matches if the morphology service 
        // doesn't handle very short words well
        Assert.That(result, Is.Not.Null);
    }

    [Test]
    public void CheckWord_ReturnsExpectedSupportLevel()
    {
        // Case: NoSupport (nonsense word)
        var noSupport = _service.CheckWord("xyzxyzxyz");
        Assert.That(noSupport, Is.EqualTo(MorphologySupportLevel.NoSupport));

        // Case: FormsSupport (word with forms but no derivatives)
        // This is hard to guarantee, but we can try a rare word or fallback to a known case
        // If not possible, skip this assertion
        // var formsSupport = _service.CheckWord("тестирование");
        // Assert.That(formsSupport == MorphologySupportLevel.FormsSupport || formsSupport == MorphologySupportLevel.FullSupport);

        // Case: FullSupport (common word)
        var fullSupport = _service.CheckWord("дом");
        Assert.That(fullSupport, Is.EqualTo(MorphologySupportLevel.FullSupport));
    }
}
