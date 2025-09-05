using Logibooks.Core.Data;
using Logibooks.Core.Models;
using Logibooks.Core.Services;
using Logibooks.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Moq;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System;

namespace Logibooks.Core.Tests.Services;

[TestFixture]
public class ParcelValidationServiceTests
{
    private static AppDbContext CreateContext(string? name = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(name ?? $"pvs_{System.Guid.NewGuid()}")
            .Options;
        return new AppDbContext(options);
    }

    [Test]
    public void ApplyCheckStatusTransition_MarkedByPartner_IsStable()
    {
        var marked = (int)ParcelCheckStatusCode.MarkedByPartner;
        foreach (ValidationEvent e in (ValidationEvent[])System.Enum.GetValues(typeof(ValidationEvent)))
        {
            var res = ParcelValidationService.ApplyCheckStatusTransition(marked, e);
            Assert.That(res, Is.EqualTo(marked));
        }
    }

    [Test]
    public void ApplyCheckStatusTransition_Defaults_BasicEvents()
    {
        Assert.That(ParcelValidationService.ApplyCheckStatusTransition((int)ParcelCheckStatusCode.NoIssues, ValidationEvent.StopWordFound), Is.EqualTo((int)ParcelCheckStatusCode.IssueStopWord));
        Assert.That(ParcelValidationService.ApplyCheckStatusTransition((int)ParcelCheckStatusCode.NoIssues, ValidationEvent.StopWordNotFound), Is.EqualTo((int)ParcelCheckStatusCode.NoIssues));
        Assert.That(ParcelValidationService.ApplyCheckStatusTransition((int)ParcelCheckStatusCode.NoIssues, ValidationEvent.InvalidFeacnFormat), Is.EqualTo((int)ParcelCheckStatusCode.NoIssuesStopWordsAndInvalidFeacnFormat));
        Assert.That(ParcelValidationService.ApplyCheckStatusTransition((int)ParcelCheckStatusCode.NoIssues, ValidationEvent.NonExistingFeacn), Is.EqualTo((int)ParcelCheckStatusCode.NoIssuesStopWordsAndNonexistingFeacn));
        Assert.That(ParcelValidationService.ApplyCheckStatusTransition((int)ParcelCheckStatusCode.NoIssues, ValidationEvent.FeacnCodeIssueFound), Is.EqualTo((int)ParcelCheckStatusCode.NoIssuesStopWordsAndFeacnCode));
        Assert.That(ParcelValidationService.ApplyCheckStatusTransition((int)ParcelCheckStatusCode.NoIssues, ValidationEvent.FeacnCodeCheckOk), Is.EqualTo((int)ParcelCheckStatusCode.NoIssues));
    }

    [Test]
    public void ApplyCheckStatusTransition_CrossClass_ReplacesFeacnIssue_PreservesStopWord()
    {
        // IssueFeacnCodeAndStopWord + InvalidFeacnFormat => IssueInvalidFeacnFormatAndStopWord
        var from = (int)ParcelCheckStatusCode.IssueFeacnCodeAndStopWord;
        var res = ParcelValidationService.ApplyCheckStatusTransition(from, ValidationEvent.InvalidFeacnFormat);
        Assert.That(res, Is.EqualTo((int)ParcelCheckStatusCode.IssueInvalidFeacnFormatAndStopWord));

        // IssueInvalidFeacnFormatAndStopWord + NonExistingFeacn => IssueNonexistingFeacnAndStopWord
        from = (int)ParcelCheckStatusCode.IssueInvalidFeacnFormatAndStopWord;
        res = ParcelValidationService.ApplyCheckStatusTransition(from, ValidationEvent.NonExistingFeacn);
        Assert.That(res, Is.EqualTo((int)ParcelCheckStatusCode.IssueNonexistingFeacnAndStopWord));

        // IssueNonexistingFeacnAndStopWord + FeacnCodeIssueFound => IssueFeacnCodeAndStopWord
        from = (int)ParcelCheckStatusCode.IssueNonexistingFeacnAndStopWord;
        res = ParcelValidationService.ApplyCheckStatusTransition(from, ValidationEvent.FeacnCodeIssueFound);
        Assert.That(res, Is.EqualTo((int)ParcelCheckStatusCode.IssueFeacnCodeAndStopWord));
    }

    [Test]
    public async Task ValidateKwAsync_AddsStopWordLinks_And_TransitionsStatus()
    {
        using var ctx = CreateContext();
        var sw1 = new StopWord { Id = 1, Word = "spam", MatchTypeId = (int)WordMatchTypeCode.ExactWord };
        var sw2 = new StopWord { Id = 2, Word = "malware", MatchTypeId = (int)WordMatchTypeCode.ExactWord };
        ctx.StopWords.AddRange(sw1, sw2);

        var parcel = new WbrParcel { Id = 10, RegisterId = 1, CheckStatusId = (int)ParcelCheckStatusCode.NoIssues, ProductName = "This product contains spam and other items", Description = "Contains malware" };
        ctx.Parcels.Add(parcel);
        await ctx.SaveChangesAsync();

        var morph = new MorphologySearchService();
        var morphologyContext = morph.InitializeContext(new[] { sw1, sw2 }.Where(s => s.MatchTypeId >= (int)WordMatchTypeCode.MorphologyMatchTypes));
        var wordsLookupContext = new WordsLookupContext<StopWord>(new[] { sw1, sw2 });

        var feacnMock = new Mock<IFeacnPrefixCheckService>();
        var svc = new ParcelValidationService(ctx, morph, feacnMock.Object);

        await svc.ValidateKwAsync(parcel, morphologyContext, wordsLookupContext);

        // reload
        var p = await ctx.Parcels.Include(p => p.BaseParcelStopWords).FirstAsync(p => p.Id == 10);
        var ids = p.BaseParcelStopWords.Select(l => l.StopWordId).ToList();
        Assert.That(ids, Does.Contain(1));
        Assert.That(ids, Does.Contain(2));

        // Check status transitioned to IssueStopWord from NoIssues
        Assert.That(p.CheckStatusId, Is.EqualTo(ParcelValidationService.ApplyCheckStatusTransition((int)ParcelCheckStatusCode.NoIssues, ValidationEvent.StopWordFound)));
    }

    [Test]
    public async Task ValidateKwAsync_DoesNothing_WhenMarkedByPartner()
    {
        using var ctx = CreateContext();
        var sw = new StopWord { Id = 1, Word = "spam", MatchTypeId = (int)WordMatchTypeCode.ExactWord };
        ctx.StopWords.Add(sw);
        var parcel = new WbrParcel { Id = 11, RegisterId = 1, CheckStatusId = (int)ParcelCheckStatusCode.MarkedByPartner, ProductName = "spam" };
        ctx.Parcels.Add(parcel);
        await ctx.SaveChangesAsync();

        var morph = new MorphologySearchService();
        var morphologyContext = morph.InitializeContext(new[] { sw });
        var wordsLookupContext = new WordsLookupContext<StopWord>(new[] { sw });

        var feacnMock = new Mock<IFeacnPrefixCheckService>();
        var svc = new ParcelValidationService(ctx, morph, feacnMock.Object);

        await svc.ValidateKwAsync(parcel, morphologyContext, wordsLookupContext);

        var p = await ctx.Parcels.Include(p => p.BaseParcelStopWords).FirstAsync(p => p.Id == 11);
        Assert.That(p.BaseParcelStopWords, Is.Empty);
        Assert.That(p.CheckStatusId, Is.EqualTo((int)ParcelCheckStatusCode.MarkedByPartner));
    }

    [Test]
    public async Task ValidateFeacnAsync_InvalidFormat_SetsInvalidFormat()
    {
        using var ctx = CreateContext();
        var parcel = new WbrParcel { Id = 20, RegisterId = 1, CheckStatusId = (int)ParcelCheckStatusCode.NoIssues, TnVed = "123" };
        ctx.Parcels.Add(parcel);
        await ctx.SaveChangesAsync();

        var feacnMock = new Mock<IFeacnPrefixCheckService>();
        var svc = new ParcelValidationService(ctx, new MorphologySearchService(), feacnMock.Object);

        await svc.ValidateFeacnAsync(parcel);

        var p = await ctx.Parcels.FirstAsync(p => p.Id == 20);
        Assert.That(p.CheckStatusId, Is.EqualTo(ParcelValidationService.ApplyCheckStatusTransition((int)ParcelCheckStatusCode.NoIssues, ValidationEvent.InvalidFeacnFormat)));
        Assert.That(ctx.Set<BaseParcelFeacnPrefix>().Any(), Is.False);
    }

    [Test]
    public async Task ValidateFeacnAsync_NonExisting_SetsNonExisting()
    {
        using var ctx = CreateContext();
        var parcel = new WbrParcel { Id = 21, RegisterId = 1, CheckStatusId = (int)ParcelCheckStatusCode.NoIssues, TnVed = "1234567890" };
        ctx.Parcels.Add(parcel);
        await ctx.SaveChangesAsync();

        var feacnMock = new Mock<IFeacnPrefixCheckService>();
        var svc = new ParcelValidationService(ctx, new MorphologySearchService(), feacnMock.Object);

        await svc.ValidateFeacnAsync(parcel);

        var p = await ctx.Parcels.FirstAsync(p => p.Id == 21);
        Assert.That(p.CheckStatusId, Is.EqualTo(ParcelValidationService.ApplyCheckStatusTransition((int)ParcelCheckStatusCode.NoIssues, ValidationEvent.NonExistingFeacn)));
        Assert.That(ctx.Set<BaseParcelFeacnPrefix>().Any(), Is.False);
    }

    [Test]
    public async Task ValidateFeacnAsync_WithPrefixLinks_AddsLinks_And_Transitions()
    {
        using var ctx = CreateContext();
        // Add a feacn code that's present
        ctx.FeacnCodes.Add(new FeacnCode { Id = 1, Code = "1234567890", CodeEx = "", Name = "n", NormalizedName = "n" });
        var parcel = new WbrParcel { Id = 22, RegisterId = 1, CheckStatusId = (int)ParcelCheckStatusCode.NoIssues, TnVed = "1234567890" };
        ctx.Parcels.Add(parcel);
        await ctx.SaveChangesAsync();

        var link = new BaseParcelFeacnPrefix { BaseParcelId = 22, FeacnPrefixId = 1 };
        var feacnMock = new Mock<IFeacnPrefixCheckService>();
        feacnMock.Setup(s => s.CheckParcelAsync(It.IsAny<BaseParcel>(), It.IsAny<CancellationToken>())).ReturnsAsync(new[] { link });

        var svc = new ParcelValidationService(ctx, new MorphologySearchService(), feacnMock.Object);
        await svc.ValidateFeacnAsync(parcel);

        var p = await ctx.Parcels.Include(p => p.BaseParcelFeacnPrefixes).FirstAsync(p => p.Id == 22);
        Assert.That(p.BaseParcelFeacnPrefixes, Is.Not.Empty);
        // status should reflect table transition for NoIssues + FeacnCodeIssueFound
        Assert.That(p.CheckStatusId, Is.EqualTo(ParcelValidationService.ApplyCheckStatusTransition((int)ParcelCheckStatusCode.NoIssues, ValidationEvent.FeacnCodeIssueFound)));
    }

    [Test]
    public async Task ValidateFeacnAsync_WithNoPrefixLinks_SetsFeacnOk()
    {
        using var ctx = CreateContext();
        ctx.FeacnCodes.Add(new FeacnCode { Id = 2, Code = "0987654321", CodeEx = "", Name = "n", NormalizedName = "n" });
        var parcel = new WbrParcel { Id = 23, RegisterId = 1, CheckStatusId = (int)ParcelCheckStatusCode.NoIssues, TnVed = "0987654321" };
        ctx.Parcels.Add(parcel);
        await ctx.SaveChangesAsync();

        var feacnMock = new Mock<IFeacnPrefixCheckService>();
        feacnMock.Setup(s => s.CheckParcelAsync(It.IsAny<BaseParcel>(), It.IsAny<CancellationToken>())).ReturnsAsync(new List<BaseParcelFeacnPrefix>());

        var svc = new ParcelValidationService(ctx, new MorphologySearchService(), feacnMock.Object);
        await svc.ValidateFeacnAsync(parcel);

        var p = await ctx.Parcels.FirstAsync(p => p.Id == 23);
        Assert.That(p.CheckStatusId, Is.EqualTo(ParcelValidationService.ApplyCheckStatusTransition((int)ParcelCheckStatusCode.NoIssues, ValidationEvent.FeacnCodeCheckOk)));
    }

    [Test]
    public async Task ValidateFeacnAsync_DoesNothing_WhenMarkedByPartner()
    {
        using var ctx = CreateContext();
        ctx.FeacnCodes.Add(new FeacnCode { Id = 3, Code = "1111111111", CodeEx = "", Name = "n", NormalizedName = "n" });
        var parcel = new WbrParcel { Id = 24, RegisterId = 1, CheckStatusId = (int)ParcelCheckStatusCode.MarkedByPartner, TnVed = "1111111111" };
        ctx.Parcels.Add(parcel);
        await ctx.SaveChangesAsync();

        var feacnMock = new Mock<IFeacnPrefixCheckService>();
        var svc = new ParcelValidationService(ctx, new MorphologySearchService(), feacnMock.Object);
        await svc.ValidateFeacnAsync(parcel);

        var p = await ctx.Parcels.FirstAsync(p => p.Id == 24);
        Assert.That(p.CheckStatusId, Is.EqualTo((int)ParcelCheckStatusCode.MarkedByPartner));
        Assert.That(ctx.Set<BaseParcelFeacnPrefix>().Any(), Is.False);
    }
}
