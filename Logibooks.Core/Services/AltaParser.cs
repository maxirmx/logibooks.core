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
// ``AS IS'' AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED
// TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR
// PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDERS OR CONTRIBUTORS
// BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
// CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
// SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
// INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
// CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
// ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
// POSSIBILITY OF SUCH DAMAGE.

using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Logibooks.Core.Models;

namespace Logibooks.Core.Services;

public static partial class AltaParser
{
    [GeneratedRegex(@"(\d{4}\s\d{2}\s\d{3}\s\d)", RegexOptions.Compiled)]
    private static partial Regex CodePattern();

    [GeneratedRegex(@"(\d{3})/?$")]
    private static partial Regex UrlNumberPattern();

    [GeneratedRegex(@".*?\(")]
    private static partial Regex ExceptionCodePrefix();

    [GeneratedRegex(@"[^\d,]+")]
    private static partial Regex NonDigitComma();

    private static readonly HttpClient SharedHttpClient = new HttpClient();

    public static Task<(List<AltaItem> Items, List<AltaException> Exceptions)> ParseAsync(IEnumerable<string> urls, HttpClient? client = null)
    {
        return Task.FromResult((new List<AltaItem>(), new List<AltaException>()));
    }
}
