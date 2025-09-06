// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application

using Logibooks.Core.Data;
using Logibooks.Core.Interfaces;
using Logibooks.Core.Models;
using System.Text.RegularExpressions;

namespace Logibooks.Core.Services;

public enum ValidationEvent
{
    StopWordFound,
    StopWordNotFound,
    InvalidFeacnFormat,
    NonExistingFeacn,
    FeacnCodeIssueFound,
    FeacnCodeCheckOk
}

public class ParcelValidationService(
    AppDbContext db, 
    IMorphologySearchService morphService, 
    IFeacnPrefixCheckService feacnPrefixCheckService) : IParcelValidationService
{
    private readonly AppDbContext _db = db;
    private readonly IMorphologySearchService _morphService = morphService;
    private readonly IFeacnPrefixCheckService _feacnPrefixCheckService = feacnPrefixCheckService;
    private static readonly Regex TnVedRegex = new($"^\\d{{{FeacnCode.FeacnCodeLength}}}$", RegexOptions.Compiled);

    /// <summary>
    /// Применяет переходы статуса проверки CheckStatusId на основе событий валидации, используя таблицу поиска.
    /// Заменяет логику на основе switch на более поддерживаемый подход, управляемый таблицей.
    /// 
    /// ЛОГИКА ПЕРЕХОДОВ СТАТУСОВ ПРОВЕРКИ:
    /// 
    /// Система статусов работает по двум измерениям:
    /// 1. СТОП-СЛОВА (Stop Words) - наличие запрещенных слов в товаре
    /// 2. КОДЫ ТН ВЭД - проблемы с кодами ТН ВЭД (формат, валидность, стоп префиксы)
    /// 
    /// ОСНОВНЫЕ ПРИНЦИПЫ:
    /// - Статус "MarkedByPartner" (200) никогда не изменяется - это финальное состояние
    /// - Все остальные статусы могут переходить в зависимости от результата валидации
    /// - При обнаружении проблем статус переходит к более специфичному варианту
    /// - При решении проблем статус возвращается к менее проблемному состоянию
    /// - КРИТИЧНО: Переходы по измерениям независимы - удаление стоп-слов не должно очищать проблемы и наоборот
    /// 
    /// </summary>
    /// <param name="currentCheckStatusId">Текущий статус проверки посылки</param>
    /// <param name="validationEvent">Событие валидации, которое произошло</param>
    /// <returns>Новый CheckStatusId после применения перехода</returns>
    public static int ApplyCheckStatusTransition(int currentCheckStatusId, ValidationEvent validationEvent)
    {
        // State transition table: (CurrentStatus, Event) -> NewStatus
        var transitionTable = new Dictionary<(int currentStatus, ValidationEvent evt), int>
        {
            // Stop word found transitions (Запрет)
            {((int)ParcelCheckStatusCode.NoIssuesFeacn, ValidationEvent.StopWordFound), (int)ParcelCheckStatusCode.NoIssuesFeacnAndStopWord},
            {((int)ParcelCheckStatusCode.NoIssuesStopWordsAndFeacnCode, ValidationEvent.StopWordFound), (int)ParcelCheckStatusCode.IssueFeacnCodeAndStopWord},
            {((int)ParcelCheckStatusCode.IssueFeacnCode, ValidationEvent.StopWordFound), (int)ParcelCheckStatusCode.IssueFeacnCodeAndStopWord},
            {((int)ParcelCheckStatusCode.IssueFeacnCodeAndStopWord, ValidationEvent.StopWordFound), (int)ParcelCheckStatusCode.IssueFeacnCodeAndStopWord},
            {((int)ParcelCheckStatusCode.IssueInvalidFeacnFormat, ValidationEvent.StopWordFound), (int)ParcelCheckStatusCode.IssueInvalidFeacnFormatAndStopWord},
            {((int)ParcelCheckStatusCode.NoIssuesStopWordsAndInvalidFeacnFormat, ValidationEvent.StopWordFound), (int)ParcelCheckStatusCode.IssueInvalidFeacnFormatAndStopWord},
            {((int)ParcelCheckStatusCode.IssueInvalidFeacnFormatAndStopWord, ValidationEvent.StopWordFound), (int)ParcelCheckStatusCode.IssueInvalidFeacnFormatAndStopWord},
            {((int)ParcelCheckStatusCode.NoIssuesStopWordsAndNonexistingFeacn, ValidationEvent.StopWordFound), (int)ParcelCheckStatusCode.IssueNonexistingFeacnAndStopWord},
            {((int)ParcelCheckStatusCode.IssueNonexistingFeacn, ValidationEvent.StopWordFound), (int)ParcelCheckStatusCode.IssueNonexistingFeacnAndStopWord},
            {((int)ParcelCheckStatusCode.IssueNonexistingFeacnAndStopWord, ValidationEvent.StopWordFound), (int)ParcelCheckStatusCode.IssueNonexistingFeacnAndStopWord},

            // Stop word not found transitions (Нет запрета)
            {((int)ParcelCheckStatusCode.NoIssues, ValidationEvent.StopWordNotFound), (int)ParcelCheckStatusCode.NoIssues},
            {((int)ParcelCheckStatusCode.NoIssuesFeacn, ValidationEvent.StopWordNotFound), (int)ParcelCheckStatusCode.NoIssues},
            {((int)ParcelCheckStatusCode.NoIssuesFeacnAndStopWord, ValidationEvent.StopWordNotFound), (int)ParcelCheckStatusCode.NoIssues},
            {((int)ParcelCheckStatusCode.IssueFeacnCode, ValidationEvent.StopWordNotFound), (int)ParcelCheckStatusCode.NoIssuesStopWordsAndFeacnCode},
            {((int)ParcelCheckStatusCode.IssueFeacnCodeAndStopWord, ValidationEvent.StopWordNotFound), (int)ParcelCheckStatusCode.NoIssuesStopWordsAndFeacnCode},
            {((int)ParcelCheckStatusCode.NoIssuesStopWordsAndFeacnCode, ValidationEvent.StopWordNotFound), (int)ParcelCheckStatusCode.NoIssuesStopWordsAndFeacnCode}, // Preserve FEACN issue
            {((int)ParcelCheckStatusCode.IssueInvalidFeacnFormat, ValidationEvent.StopWordNotFound), (int)ParcelCheckStatusCode.NoIssuesStopWordsAndInvalidFeacnFormat},
            {((int)ParcelCheckStatusCode.IssueInvalidFeacnFormatAndStopWord, ValidationEvent.StopWordNotFound), (int)ParcelCheckStatusCode.NoIssuesStopWordsAndInvalidFeacnFormat},
            {((int)ParcelCheckStatusCode.NoIssuesStopWordsAndInvalidFeacnFormat, ValidationEvent.StopWordNotFound), (int)ParcelCheckStatusCode.NoIssuesStopWordsAndInvalidFeacnFormat}, // Preserve FEACN issue
            {((int)ParcelCheckStatusCode.IssueNonexistingFeacn, ValidationEvent.StopWordNotFound), (int)ParcelCheckStatusCode.NoIssuesStopWordsAndNonexistingFeacn},
            {((int)ParcelCheckStatusCode.IssueNonexistingFeacnAndStopWord, ValidationEvent.StopWordNotFound), (int)ParcelCheckStatusCode.NoIssuesStopWordsAndNonexistingFeacn},
            {((int)ParcelCheckStatusCode.NoIssuesStopWordsAndNonexistingFeacn, ValidationEvent.StopWordNotFound), (int)ParcelCheckStatusCode.NoIssuesStopWordsAndNonexistingFeacn}, // Preserve FEACN issue

            // Invalid FEACN format transitions (Запрет)
            {((int)ParcelCheckStatusCode.NoIssues, ValidationEvent.InvalidFeacnFormat), (int)ParcelCheckStatusCode.NoIssuesStopWordsAndInvalidFeacnFormat},
            {((int)ParcelCheckStatusCode.NoIssuesStopWords, ValidationEvent.InvalidFeacnFormat), (int)ParcelCheckStatusCode.NoIssuesStopWordsAndInvalidFeacnFormat},
            {((int)ParcelCheckStatusCode.IssueStopWord, ValidationEvent.InvalidFeacnFormat), (int)ParcelCheckStatusCode.IssueInvalidFeacnFormatAndStopWord},
            {((int)ParcelCheckStatusCode.IssueInvalidFeacnFormatAndStopWord, ValidationEvent.InvalidFeacnFormat), (int)ParcelCheckStatusCode.IssueInvalidFeacnFormatAndStopWord},

            // Non-existing FEACN transitions (Запрет)
            {((int)ParcelCheckStatusCode.NoIssues, ValidationEvent.NonExistingFeacn), (int)ParcelCheckStatusCode.NoIssuesStopWordsAndNonexistingFeacn},
            {((int)ParcelCheckStatusCode.NoIssuesStopWords, ValidationEvent.NonExistingFeacn), (int)ParcelCheckStatusCode.NoIssuesStopWordsAndNonexistingFeacn},
            {((int)ParcelCheckStatusCode.IssueStopWord, ValidationEvent.NonExistingFeacn), (int)ParcelCheckStatusCode.IssueNonexistingFeacnAndStopWord},
            {((int)ParcelCheckStatusCode.IssueNonexistingFeacnAndStopWord, ValidationEvent.NonExistingFeacn), (int)ParcelCheckStatusCode.IssueNonexistingFeacnAndStopWord},

            // FEACN code issue found transitions (Запрет)
            {((int)ParcelCheckStatusCode.NoIssues, ValidationEvent.FeacnCodeIssueFound), (int)ParcelCheckStatusCode.NoIssuesStopWordsAndFeacnCode},
            {((int)ParcelCheckStatusCode.NoIssuesStopWords, ValidationEvent.FeacnCodeIssueFound), (int)ParcelCheckStatusCode.NoIssuesStopWordsAndFeacnCode},
            {((int)ParcelCheckStatusCode.IssueStopWord, ValidationEvent.FeacnCodeIssueFound), (int)ParcelCheckStatusCode.IssueFeacnCodeAndStopWord},
            {((int)ParcelCheckStatusCode.IssueFeacnCodeAndStopWord, ValidationEvent.FeacnCodeIssueFound), (int)ParcelCheckStatusCode.IssueFeacnCodeAndStopWord},

            // FEACN code check OK transitions 
            {((int)ParcelCheckStatusCode.NoIssues, ValidationEvent.FeacnCodeCheckOk), (int)ParcelCheckStatusCode.NoIssues},
            {((int)ParcelCheckStatusCode.NoIssuesStopWords, ValidationEvent.FeacnCodeCheckOk), (int)ParcelCheckStatusCode.NoIssues},
            {((int)ParcelCheckStatusCode.NoIssuesStopWordsAndFeacnCode, ValidationEvent.FeacnCodeCheckOk), (int)ParcelCheckStatusCode.NoIssues},
            {((int)ParcelCheckStatusCode.NoIssuesStopWordsAndInvalidFeacnFormat, ValidationEvent.FeacnCodeCheckOk), (int)ParcelCheckStatusCode.NoIssues},
            {((int)ParcelCheckStatusCode.NoIssuesStopWordsAndNonexistingFeacn, ValidationEvent.FeacnCodeCheckOk), (int)ParcelCheckStatusCode.NoIssues},
            {((int)ParcelCheckStatusCode.IssueStopWord, ValidationEvent.FeacnCodeCheckOk), (int)ParcelCheckStatusCode.NoIssuesFeacnAndStopWord},
            {((int)ParcelCheckStatusCode.NoIssuesFeacnAndStopWord, ValidationEvent.FeacnCodeCheckOk), (int)ParcelCheckStatusCode.NoIssuesFeacnAndStopWord},
            {((int)ParcelCheckStatusCode.IssueFeacnCodeAndStopWord, ValidationEvent.FeacnCodeCheckOk), (int)ParcelCheckStatusCode.NoIssuesFeacnAndStopWord},
            {((int)ParcelCheckStatusCode.IssueInvalidFeacnFormatAndStopWord, ValidationEvent.FeacnCodeCheckOk), (int)ParcelCheckStatusCode.NoIssuesFeacnAndStopWord},
            {((int)ParcelCheckStatusCode.IssueNonexistingFeacnAndStopWord, ValidationEvent.FeacnCodeCheckOk), (int)ParcelCheckStatusCode.NoIssuesFeacnAndStopWord},

            // Cross-class FEACN issue replacement transitions (preserve stop word dimension)

            // FeacnCode -> InvalidFormat
            {((int)ParcelCheckStatusCode.IssueFeacnCode, ValidationEvent.InvalidFeacnFormat), (int)ParcelCheckStatusCode.IssueInvalidFeacnFormat},
            {((int)ParcelCheckStatusCode.IssueFeacnCodeAndStopWord, ValidationEvent.InvalidFeacnFormat), (int)ParcelCheckStatusCode.IssueInvalidFeacnFormatAndStopWord},
            {((int)ParcelCheckStatusCode.NoIssuesStopWordsAndFeacnCode, ValidationEvent.InvalidFeacnFormat), (int)ParcelCheckStatusCode.NoIssuesStopWordsAndInvalidFeacnFormat},

            // FeacnCode -> NonExisting
            {((int)ParcelCheckStatusCode.IssueFeacnCode, ValidationEvent.NonExistingFeacn), (int)ParcelCheckStatusCode.IssueNonexistingFeacn},
            {((int)ParcelCheckStatusCode.IssueFeacnCodeAndStopWord, ValidationEvent.NonExistingFeacn), (int)ParcelCheckStatusCode.IssueNonexistingFeacnAndStopWord},
            {((int)ParcelCheckStatusCode.NoIssuesStopWordsAndFeacnCode, ValidationEvent.NonExistingFeacn), (int)ParcelCheckStatusCode.NoIssuesStopWordsAndNonexistingFeacn},

            // InvalidFormat -> NonExisting
            {((int)ParcelCheckStatusCode.IssueInvalidFeacnFormat, ValidationEvent.NonExistingFeacn), (int)ParcelCheckStatusCode.IssueNonexistingFeacn},
            {((int)ParcelCheckStatusCode.IssueInvalidFeacnFormatAndStopWord, ValidationEvent.NonExistingFeacn), (int)ParcelCheckStatusCode.IssueNonexistingFeacnAndStopWord},
            {((int)ParcelCheckStatusCode.NoIssuesStopWordsAndInvalidFeacnFormat, ValidationEvent.NonExistingFeacn), (int)ParcelCheckStatusCode.NoIssuesStopWordsAndNonexistingFeacn},

            // NonExisting -> InvalidFormat
            {((int)ParcelCheckStatusCode.IssueNonexistingFeacn, ValidationEvent.InvalidFeacnFormat), (int)ParcelCheckStatusCode.IssueInvalidFeacnFormat},
            {((int)ParcelCheckStatusCode.IssueNonexistingFeacnAndStopWord, ValidationEvent.InvalidFeacnFormat), (int)ParcelCheckStatusCode.IssueInvalidFeacnFormatAndStopWord},
            {((int)ParcelCheckStatusCode.NoIssuesStopWordsAndNonexistingFeacn, ValidationEvent.InvalidFeacnFormat), (int)ParcelCheckStatusCode.NoIssuesStopWordsAndInvalidFeacnFormat},

            // InvalidFormat -> FeacnCodeIssueFound
            {((int)ParcelCheckStatusCode.IssueInvalidFeacnFormat, ValidationEvent.FeacnCodeIssueFound), (int)ParcelCheckStatusCode.IssueFeacnCode},
            {((int)ParcelCheckStatusCode.IssueInvalidFeacnFormatAndStopWord, ValidationEvent.FeacnCodeIssueFound), (int)ParcelCheckStatusCode.IssueFeacnCodeAndStopWord},
            {((int)ParcelCheckStatusCode.NoIssuesStopWordsAndInvalidFeacnFormat, ValidationEvent.FeacnCodeIssueFound), (int)ParcelCheckStatusCode.NoIssuesStopWordsAndFeacnCode},

            // NonExisting -> FeacnCodeIssueFound
            {((int)ParcelCheckStatusCode.IssueNonexistingFeacn, ValidationEvent.FeacnCodeIssueFound), (int)ParcelCheckStatusCode.IssueFeacnCode},
            {((int)ParcelCheckStatusCode.IssueNonexistingFeacnAndStopWord, ValidationEvent.FeacnCodeIssueFound), (int)ParcelCheckStatusCode.IssueFeacnCodeAndStopWord},
            {((int)ParcelCheckStatusCode.NoIssuesStopWordsAndNonexistingFeacn, ValidationEvent.FeacnCodeIssueFound), (int)ParcelCheckStatusCode.NoIssuesStopWordsAndFeacnCode},
        };

        // Default transitions for events that set specific issues when no specific rule exists
        var defaultTransitions = new Dictionary<ValidationEvent, int>
        {
            [ValidationEvent.StopWordFound] = (int)ParcelCheckStatusCode.IssueStopWord,
            [ValidationEvent.StopWordNotFound] = (int)ParcelCheckStatusCode.NoIssuesStopWords,
            [ValidationEvent.InvalidFeacnFormat] = (int)ParcelCheckStatusCode.IssueInvalidFeacnFormat,
            [ValidationEvent.NonExistingFeacn] = (int)ParcelCheckStatusCode.IssueNonexistingFeacn,
            [ValidationEvent.FeacnCodeIssueFound] = (int)ParcelCheckStatusCode.IssueFeacnCode,
            [ValidationEvent.FeacnCodeCheckOk] = (int)ParcelCheckStatusCode.NoIssuesFeacn
        };

        // Special case: MarkedByPartner status never changes
        if (currentCheckStatusId == (int)ParcelCheckStatusCode.MarkedByPartner)
        {
            return currentCheckStatusId;
        }

        // Look up specific transition first
        if (transitionTable.TryGetValue((currentCheckStatusId, validationEvent), out var newStatus))
        {
            return newStatus;
        }

        // Fall back to default transition for the event
        if (defaultTransitions.TryGetValue(validationEvent, out var defaultStatus))
        {
            return defaultStatus;
        }

        // If no transition is defined, return current status unchanged
        return currentCheckStatusId;
    }

    public async Task ValidateSwAsync(
        BaseParcel order,
        MorphologyContext morphologyContext,
        WordsLookupContext<StopWord> wordsLookupContext,
        CancellationToken cancellationToken = default)
    {
        await ValidateSwAsync(_db, order, morphologyContext, wordsLookupContext, cancellationToken);
    }

    public async Task ValidateSwAsync(
        AppDbContext dbContext,
        BaseParcel order,
        MorphologyContext morphologyContext,
        WordsLookupContext<StopWord> wordsLookupContext,
        CancellationToken cancellationToken = default)
    {
        if (order.CheckStatusId == (int)ParcelCheckStatusCode.MarkedByPartner)
        {
            return;
        }

        // For in-memory database compatibility, perform operations in a specific order to maintain consistency
        // Store original state in case we need to revert
        var originalCheckStatusId = order.CheckStatusId;
        Exception? rollbackException = null;

        try
        {
            // Step 1: Remove existing stop word links
            var existing = dbContext.Set<BaseParcelStopWord>().Where(l => l.BaseParcelId == order.Id);
            var existingLinks = existing.ToList(); // Store for potential rollback
            dbContext.Set<BaseParcelStopWord>().RemoveRange(existing);

            // Step 2: Calculate new stop word links
            var productName = order.ProductName ?? string.Empty;
            var links = SelectStopWordLinks(order.Id, productName, wordsLookupContext, morphologyContext);

            if (order is WbrParcel wbr && !string.IsNullOrWhiteSpace(wbr.Description))
            {
                var linksDesc = SelectStopWordLinks(order.Id, wbr.Description, wordsLookupContext, morphologyContext);
                var existingIds = new HashSet<int>(links.Select(l => l.StopWordId));
                foreach (var link in linksDesc)
                {
                    if (existingIds.Add(link.StopWordId))
                    {
                        links.Add(link);
                    }
                }
            }

            // Step 3: Add new links and update status
            if (links.Count > 0)
            {
                dbContext.AddRange(links);
                // Use table-driven transition
                order.CheckStatusId = ApplyCheckStatusTransition(order.CheckStatusId, ValidationEvent.StopWordFound);
            }
            else
            {
                // Use table-driven transition
                order.CheckStatusId = ApplyCheckStatusTransition(order.CheckStatusId, ValidationEvent.StopWordNotFound);
            }

            // Step 4: Save all changes atomically
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            rollbackException = ex;
            
            // Manual rollback for in-memory database: restore original state
            try
            {
                order.CheckStatusId = originalCheckStatusId;
                
                // Remove any partially added links
                var partialLinks = dbContext.Set<BaseParcelStopWord>().Where(l => l.BaseParcelId == order.Id);
                dbContext.Set<BaseParcelStopWord>().RemoveRange(partialLinks);
                
                // Note: For in-memory database, we can't restore the original links reliably
                // The calling code should handle this by reloading the parcel if needed
                
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch
            {
                // If rollback fails, the original exception is more important
            }
            
            throw rollbackException;
        }
    }

    public async Task ValidateFeacnAsync(
        BaseParcel parcel,
        FeacnPrefixCheckContext? feacnContext = null,
        CancellationToken cancellationToken = default)
    {
        await ValidateFeacnAsync(_db, parcel, feacnContext, cancellationToken);
    }

    public async Task ValidateFeacnAsync(
        AppDbContext dbContext,
        BaseParcel parcel,
        FeacnPrefixCheckContext? feacnContext = null,
        CancellationToken cancellationToken = default)
    {
        if (parcel.CheckStatusId == (int)ParcelCheckStatusCode.MarkedByPartner)
        {
            return;
        }

        // For in-memory database compatibility, perform operations in a specific order to maintain consistency
        // Store original state in case we need to revert
        var originalCheckStatusId = parcel.CheckStatusId;
        Exception? rollbackException = null;

        try
        {
            // Step 1: Remove existing FEACN prefix links
            var existing = dbContext.Set<BaseParcelFeacnPrefix>().Where(l => l.BaseParcelId == parcel.Id);
            var existingLinks = existing.ToList(); // Store for potential rollback
            dbContext.Set<BaseParcelFeacnPrefix>().RemoveRange(existing);

            // Step 2: Validate FEACN and determine new status
            if (string.IsNullOrWhiteSpace(parcel.TnVed) || !TnVedRegex.IsMatch(parcel.TnVed))
            {
                // Use table-driven transition
                parcel.CheckStatusId = ApplyCheckStatusTransition(parcel.CheckStatusId, ValidationEvent.InvalidFeacnFormat);
            }
            else if (!dbContext.FeacnCodes.Any(f => f.Code == parcel.TnVed))
            {
                // Use table-driven transition
                parcel.CheckStatusId = ApplyCheckStatusTransition(parcel.CheckStatusId, ValidationEvent.NonExistingFeacn);
            }
            else
            {
                // Step 3: Check for FEACN prefix matches
                var links = feacnContext != null
                    ? _feacnPrefixCheckService.CheckParcel(parcel, feacnContext)
                    : await _feacnPrefixCheckService.CheckParcelAsync(parcel, cancellationToken);

                if (links.Any())
                {
                    dbContext.AddRange(links);
                    // Use table-driven transition
                    parcel.CheckStatusId = ApplyCheckStatusTransition(parcel.CheckStatusId, ValidationEvent.FeacnCodeIssueFound);
                }
                else
                {
                    // Use table-driven transition
                    parcel.CheckStatusId = ApplyCheckStatusTransition(parcel.CheckStatusId, ValidationEvent.FeacnCodeCheckOk);
                }
            }
        
            // Step 4: Save all changes atomically
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            rollbackException = ex;
            
            // Manual rollback for in-memory database: restore original state
            try
            {
                parcel.CheckStatusId = originalCheckStatusId;
                
                // Remove any partially added links
                var partialLinks = dbContext.Set<BaseParcelFeacnPrefix>().Where(l => l.BaseParcelId == parcel.Id);
                dbContext.Set<BaseParcelFeacnPrefix>().RemoveRange(partialLinks);
                
                // Note: For in-memory database, we can't restore the original links reliably
                // The calling code should handle this by reloading the parcel if needed
                
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch
            {
                // If rollback fails, the original exception is more important
            }
            
            throw rollbackException;
        }
    }

    private List<BaseParcelStopWord> SelectStopWordLinks(
        int orderId,
        string productName,
        WordsLookupContext<StopWord> wordsLookupContext,
        MorphologyContext morphologyContext)
    {
        var links = new List<BaseParcelStopWord>();
        var existingStopWordIds = new HashSet<int>();

        var matchingWords = wordsLookupContext.GetMatchingWords(productName);

        // Add stop words to links
        foreach (var sw in matchingWords)
        {
            links.Add(new BaseParcelStopWord { BaseParcelId = orderId, StopWordId = sw.Id });
            existingStopWordIds.Add(sw.Id);
        }

        var ids = _morphService.CheckText(morphologyContext, productName);
        foreach (var id in ids)
        {
            if (existingStopWordIds.Add(id)) // HashSet.Add returns false if already exists
                links.Add(new BaseParcelStopWord { BaseParcelId = orderId, StopWordId = id });
        }

        return links;
    }
}
