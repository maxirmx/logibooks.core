// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application

using ClosedXML.Excel;
using Logibooks.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace Logibooks.Core.Tests.Services;

public class ExcelColorParserTests
{
    private ILogger _logger = new Mock<ILogger>().Object;

    [Test]
    public void GetRowColor_ReturnsTrue_WithThemeColor()
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.AddWorksheet();
        ws.Cell(1, 1).Style.Fill.BackgroundColor = XLColor.FromTheme(XLThemeColor.Accent2);

        var (hasColor, color) = ExcelColorParser.GetRowColor(ws, 1, _logger);

        Assert.That(hasColor, Is.True);
        Assert.That(color, Is.Not.Null);
        Assert.That(color!.Color.ToArgb(), Is.EqualTo(XLColor.FromArgb(192, 80, 77).Color.ToArgb()));
    }

    [Test]
    public void GetRowColor_ReturnsFalse_WhenNoColor()
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.AddWorksheet();
        ws.Cell(1, 1).Style.Fill.BackgroundColor = XLColor.White;

        var (hasColor, color) = ExcelColorParser.GetRowColor(ws, 1, _logger);

        Assert.That(hasColor, Is.False);
        Assert.That(color, Is.Null);
    }

    [Test]
    public void ConvertToRgbColor_AppliesTint()
    {
        var themeColor = XLColor.FromTheme(XLThemeColor.Accent1, 0.5);

        var rgb = ExcelColorParser.ConvertToRgbColor(themeColor, _logger);

        Assert.That(rgb.Color.R, Is.EqualTo(98));
        Assert.That(rgb.Color.G, Is.EqualTo(161));
        Assert.That(rgb.Color.B, Is.EqualTo(236));
    }
}

