using System.Collections.Generic;
using System.Threading.Tasks;
using Logibooks.Core.Services;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace Logibooks.Core.Tests.Services;

[TestFixture]
public class MorphSearchServiceTests
{
    [Test]
    public async Task InitializeAsync_ReturnsKeywordLemmas()
    {
        var svc = new MorphSearchService();
        var lemmas = await svc.InitializeAsync(new []
        {
            new SearchKeyword(1, "кошки"),
            new SearchKeyword(2, "собаки")
        });

        CollectionAssert.AreEqual(new []{"кошка","собака"}, lemmas);
    }

    [Test]
    public async Task CheckTextAsync_FindsKeywords()
    {
        var svc = new MorphSearchService();
        await svc.InitializeAsync(new [] { new SearchKeyword(10, "кошка"), new SearchKeyword(11, "дом") });

        var ids = await svc.CheckTextAsync("Кошки гуляют по домам");

        CollectionAssert.AreEquivalent(new []{10,11}, ids);
    }
}
