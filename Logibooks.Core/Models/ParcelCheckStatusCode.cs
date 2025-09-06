// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application

namespace Logibooks.Core.Models;
public enum ParcelCheckStatusCode
{
    NotChecked = 1,
    // Legacy code, not used anymore
    // Используется IssueFeacnCode, IssueStopWord, IssueFeacnCodeAndStopWord, ...
    HasIssues = 101,
    // Legacy code, not used anymore
    InvalidFeacnFormat = 102,
    // Legacy code, not used anymore
    NonexistingFeacn = 103,

    // New values
    IssueFeacnCode = 128+1,
    IssueFeacnCodeAndStopWord = 128 + 2,
    IssueNonexistingFeacn = 128 + 3,
    IssueNonexistingFeacnAndStopWord = 128 + 4,
    IssueInvalidFeacnFormat = 128 + 5,
    IssueInvalidFeacnFormatAndStopWord = 128 + 6,
    IssueStopWord = 128+7,
    NoIssuesFeacnAndStopWord = 128 + 8,
    NoIssuesStopWordsAndFeacnCode = 128 + 9,
    NoIssuesStopWordsAndNonexistingFeacn = 128 + 10,
    NoIssuesStopWordsAndInvalidFeacnFormat = 128 + 11,
    //

    MarkedByPartner = 200,
    NoIssues = 201,
    NoIssuesStopWords = 202,
    NoIssuesFeacn = 203,
    Approved = 301,
    ApprovedWithExcise = 399,
}
