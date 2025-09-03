// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application

namespace Logibooks.Core.Models;
public enum ParcelCheckStatusCode
{
    NotChecked = 1,
    // Legacy code, not used anymore
    // ������������ BlockedByFeacnCode, BlockedByStopWord, BlockedByFeacnCodeAndStopWord, ...
    HasIssues = 101,
    // Legacy code, not used anymore
    InvalidFeacnFormat = 102,
    // Legacy code, not used anymore
    NonexistingFeacn = 103,
    // ��������� ������� ������� ����� �� 128
    // 128     = 10000000
    // 128+1   = 10000001  (�������� �� ���� �� ���)
    // 128+2   = 10000010  (�������� �� ����-������)
    // 128+1+2 = 10000011  (�������� � �� ���� �� ���, � �� ����-������)
    // 128+4   = 10000100  (�������������� ��� �� ���)
    // 128+4+2 = 10000110  (�������������� ��� �� ��� � �������� �� ����-������)
    // 128+8   = 10001000  (�������� ������ ���� �� ���)
    // 128+8+2 = 10001010  (�������� ������ ���� �� ��� � �������� �� ����-������)
    // ��� ����� ����� ������ �����
    BlockedByFeacnCode = 128+1,
    BlockedByStopWord = 128+2,
    BlockedByFeacnCodeAndStopWord = 128+1+2,
    BlockedByNonexistingFeacn = 128+4,
    BlockedByNonexistingFeacnAndStopWord = 128+2+4,
    BlockedByInvalidFeacnFormat = 128+8,
    BlockedByInvalidFeacnFormatAndStopWord = 128+2+8,
    //
    MarkedByPartner = 200,
    NoIssues = 201,
    Approved = 301,
    ApprovedWithExcise = 399,
}
