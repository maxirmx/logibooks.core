using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Logibooks.Core.Services;
using NUnit.Framework;

namespace Logibooks.Core.Tests.Services;

public class FakeHandler : HttpMessageHandler
{
    private readonly string _html;
    public FakeHandler(string html) { _html = html; }
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var resp = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(_html)
        };
        return Task.FromResult(resp);
    }
}

[TestFixture]
public class AltaParserTests
{
    [Test]
    public async Task ParseAsync_ParsesHtmlTables()
    {
        var html = @"<table>
<tr><td>Prod1</td><td>1234 56 789 0</td><td>c1</td></tr>
<tr><td>Prod2</td><td>1111 22 333 4, 2222 33 444 5</td><td></td></tr>
<tr><td>Prod3</td><td>1234 11 111 1 (за исключением 9999 99 999 9)</td><td>n</td></tr>
</table>";
        var client = new HttpClient(new FakeHandler(html));
        var (items, exceptions) = await AltaParser.ParseAsync(new[] { "https://test" }, client);
        Assert.That(items.Count, Is.EqualTo(5));
        Assert.That(exceptions.Count, Is.EqualTo(2));
    }
}
