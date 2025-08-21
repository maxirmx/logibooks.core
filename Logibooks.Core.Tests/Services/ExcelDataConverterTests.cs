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

using Logibooks.Core.Services;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using System;

namespace Logibooks.Core.Tests.Services;

public class ExcelDataConverterTests
{
    private ILogger? _logger;

    [SetUp]
    public void Setup()
    {
        _logger = new LoggerFactory().CreateLogger("ExcelDataConverterTests");
    }

    [TestCase("42", typeof(int), 42)]
    [TestCase("notanint", typeof(int), 0)]
    [TestCase("3.14", typeof(double), 3.14)]
    [TestCase("2,71", typeof(double), 2.71)] // comma as decimal separator
    [TestCase("notadouble", typeof(double), 0.0)]
    [TestCase("123.45", typeof(decimal), 123.45)]
    [TestCase("67,89", typeof(decimal), 67.89)]
    [TestCase("notadecimal", typeof(decimal), 0.0)]
    [TestCase("true", typeof(bool), true)]
    [TestCase("false", typeof(bool), false)]
    [TestCase("1", typeof(bool), true)]
    [TestCase("0", typeof(bool), false)]
    [TestCase("yeS", typeof(bool), true)]
    [TestCase("no", typeof(bool), false)]
    [TestCase("Да", typeof(bool), true)]
    [TestCase("нет", typeof(bool), false)]
    [TestCase("", typeof(bool), false)]
    [TestCase("notabool", typeof(bool), false)]
    [TestCase("2024-06-28", typeof(DateTime), "2024-06-28")]
    [TestCase("notadate", typeof(DateTime), "0001-01-01")]
    [TestCase("2024-06-28", typeof(DateOnly), "2024-06-28")]
    [TestCase("2024-06-28T13:00:12", typeof(DateOnly), "2024-06-28")]
    [TestCase("notadate", typeof(DateOnly), "0001-01-01")]
    [TestCase("hello", typeof(string), "hello")]
    [TestCase("", typeof(string), "")]
    [TestCase(null, typeof(string), "")]

    public void ConvertValueToPropertyType_PrimitiveTypes_Works(string? input, Type type, object expected)
    {
        var result = ExcelDataConverter.ConvertValueToPropertyType(input, type, "TestProp", _logger);

        if (type == typeof(DateTime))
        {
            var expectedDate = DateTime.TryParse(expected.ToString(), out var dt) ? dt : default;
            Assert.That(result, Is.EqualTo(expectedDate));
        }
        else if (type == typeof(DateOnly))
        {
            var expectedDate = DateOnly.TryParse(expected.ToString(), out var d) ? d : default;
            Assert.That(result, Is.EqualTo(expectedDate));
        }
        else
        {
            Assert.That(result, Is.EqualTo(expected));
        }
    }

    [Test]
    public void ConvertValueToPropertyType_NullableInt_ReturnsNullOnNull()
    {
        var type = typeof(int?);
        var result = ExcelDataConverter.ConvertValueToPropertyType(null, type, "TestProp", _logger);
        Assert.That(result, Is.Null);
    }

    [Test]
    public void ConvertValueToPropertyType_NullableDouble_ReturnsNullOnEmpty()
    {
        var type = typeof(double?);
        var result = ExcelDataConverter.ConvertValueToPropertyType("", type, "TestProp", _logger);
        Assert.That(result, Is.Null);
    }

    [Test]
    public void ConvertValueToPropertyType_UnknownType_UsesChangeType()
    {
        var type = typeof(long);
        var result = ExcelDataConverter.ConvertValueToPropertyType("123456789", type, "TestProp", _logger);
        Assert.That(result, Is.EqualTo(123456789L));
    }

    [Test]
    public void ConvertValueToPropertyType_UnknownType_ReturnsDefaultOnError()
    {
        var type = typeof(Guid);
        var result = ExcelDataConverter.ConvertValueToPropertyType("notaguid", type, "TestProp", _logger);
        Assert.That(result, Is.EqualTo(Guid.Empty));
    }
}