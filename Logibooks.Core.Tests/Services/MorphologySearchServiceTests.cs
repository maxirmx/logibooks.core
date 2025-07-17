using NUnit.Framework;
using System.Linq;
using Logibooks.Core.Models;
using Logibooks.Core.Services;

namespace Logibooks.Core.Tests.Services;

[TestFixture]
public class MorphologySearchServiceTests
{
    [Test]
    public void CheckText_FindsDerivativeMatch()
    {
        var svc = new MorphologySearchService();
        var sw = new StopWord { Id = 1, Word = "золото" };
        var ctx = svc.InitializeContext(new[] { sw });
        var res = svc.CheckText(ctx, "золотой браслет и алюминиевый слиток");
        Assert.That(res.Contains(1));
    }
}
