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
            new SearchKeyword(1, "золото"),
            new SearchKeyword(2, "железо")
        });

        CollectionAssert.AreEqual(new []{ "золото", "железо" }, lemmas);
    }

    [Test]
    public async Task CheckTextAsync_FindsKeywords()
    {
        var svc = new MorphSearchService();
        await svc.InitializeAsync(new [] { new SearchKeyword(10, "золото") });

        var ids = await svc.CheckTextAsync("золотой браслет");

        CollectionAssert.AreEquivalent(new []{10}, ids);
    }
}
