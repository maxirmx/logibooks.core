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

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using NUnit.Framework;
using Moq;

using Logibooks.Core.Controllers;
using Logibooks.Core.Data;
using Logibooks.Core.Models;
using Logibooks.Core.RestModels;
using Logibooks.Core.Services;
using Logibooks.Core.Interfaces;

namespace Logibooks.Core.Tests.Controllers.Registers;

[TestFixture]
public class RegistersControllerTests : RegistersControllerTestsBase
{
    [Test]
    public async Task UploadRegister_ReturnsForbidden_ForNonLogist()
    {
        SetCurrentUserId(2); // Admin user
        var mockFile = new Mock<IFormFile>();
        mockFile.Setup(f => f.Length).Returns(0);
        var result = await _controller.UploadRegister(mockFile.Object);
        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task UploadRegister_ReturnsBadRequest_WhenNoFileUploaded()
    {
        SetCurrentUserId(1); // Logist user
        var mockFile = new Mock<IFormFile>();
        mockFile.Setup(f => f.Length).Returns(0);
        var result = await _controller.UploadRegister(mockFile.Object);
        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
        var error = obj.Value as ErrMessage;
        Assert.That(error!.Msg, Does.Contain("Пустой файл реестра"));
    }

    [Test]
    public async Task UploadRegister_ReturnsBadRequest_WhenEmptyFileUploaded()
    {
        SetCurrentUserId(1); // Logist user
        var mockFile = new Mock<IFormFile>();
        mockFile.Setup(f => f.Length).Returns(0);

        var result = await _controller.UploadRegister(mockFile.Object);

        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
        var error = obj.Value as ErrMessage;
        Assert.That(error!.Msg, Does.Contain("Пустой файл реестра"));
    }

    [Test]
    public async Task UploadRegister_ReturnsBadRequest_WhenUnsupportedFileType()
    {
        SetCurrentUserId(1); // Logist user
        var mockFile = CreateMockFile("test.pdf", "application/pdf", new byte[] { 0x01 });

        var result = await _controller.UploadRegister(mockFile.Object);

        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
        var error = obj.Value as ErrMessage;
        Assert.That(error!.Msg, Does.Contain("Файлы формата .pdf не поддерживаются. Можно загрузить .xlsx, .xls, .zip, .rar"));
    }

    [Test]
    public async Task UploadRegister_ReturnsSuccess_WhenExcelFileUploaded()
    {
        SetCurrentUserId(1); // Logist user  

        string testFilePath = Path.Combine(testDataDir, "Реестр_207730349.xlsx");
        byte[] excelContent;

        try
        {
            excelContent = File.ReadAllBytes(testFilePath);
        }
        catch (Exception ex)
        {
            Assert.Fail($"Test file not found at {testFilePath}: {ex.Message}");
            return;
        }

        // Set up the mock processing service to return a successful reference
        var expectedReference = new Reference { Id = 123 };
        _mockProcessingService.Setup(x => x.UploadRegisterFromExcelAsync(
            It.IsAny<int>(), 
            It.IsAny<byte[]>(), 
            It.IsAny<string>(), 
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedReference);

        var mockFile = CreateMockFile("Реестр_207730349.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", excelContent);
        var result = await _controller.UploadRegister(mockFile.Object);
        Assert.That(result, Is.TypeOf<CreatedAtActionResult>());

        // Verify the CreatedAtActionResult properties
        var createdResult = result as CreatedAtActionResult;
        Assert.That(createdResult!.Value, Is.TypeOf<Reference>());
        var returnedReference = createdResult.Value as Reference;
        Assert.That(returnedReference!.Id, Is.EqualTo(expectedReference.Id));
    }

    [Test]
    public async Task UploadRegister_ReturnsBadRequest_WhenZipWithoutExcelUploaded()
    {
        SetCurrentUserId(1); // Logist user

        // Create a real ZIP in memory without any Excel files
        using var zipStream = new MemoryStream();
        using (var archive = new System.IO.Compression.ZipArchive(zipStream, System.IO.Compression.ZipArchiveMode.Create, true))
        {
            var entry = archive.CreateEntry("test.txt");
            using var entryStream = entry.Open();
            byte[] textContent = System.Text.Encoding.UTF8.GetBytes("Test content");
            entryStream.Write(textContent, 0, textContent.Length);
        }

        var mockFile = CreateMockFile("test.zip", "application/zip", zipStream.ToArray());

        var result = await _controller.UploadRegister(mockFile.Object);

        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
        var error = obj.Value as ErrMessage;
        Assert.That(error!.Msg, Does.Contain("Файл реестра не найден в архиве"));
    }

    [Test]
    public async Task UploadRegister_ReturnsSuccess_WhenZipWithExcelUploaded()
    {
        SetCurrentUserId(1); // Logist user

        // Load test zip file from test.data folder
        string testFilePath = Path.Combine(testDataDir, "Реестр_207730349.zip");

        byte[] zipContent;

        try
        {
            zipContent = File.ReadAllBytes(testFilePath);
        }
        catch (Exception ex)
        {
            Assert.Fail($"Test file not found at {testFilePath}: {ex.Message}");
            return;
        }

        // Set up the mock processing service to return a successful reference
        var expectedReference = new Reference { Id = 124 };
        _mockProcessingService.Setup(x => x.UploadRegisterFromExcelAsync(
            It.IsAny<int>(), 
            It.IsAny<byte[]>(), 
            It.IsAny<string>(), 
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedReference);

        var mockFile = CreateMockFile("Реестр_207730349.zip", "application/zip", zipContent);

        var result = await _controller.UploadRegister(mockFile.Object);

        // Assert that the result is OK
        Assert.That(result, Is.TypeOf<CreatedAtActionResult>());

        // Verify the CreatedAtActionResult properties
        var createdResult = result as CreatedAtActionResult;
        Assert.That(createdResult!.Value, Is.TypeOf<Reference>());
        var returnedReference = createdResult.Value as Reference;
        Assert.That(returnedReference!.Id, Is.EqualTo(expectedReference.Id));
    }

    [Test]
    public async Task ProcessExcel_ReturnsBadRequest_WhenExcelFileIsEmpty()
    {
        SetCurrentUserId(1); // Logist user  

        string testFilePath = Path.Combine(testDataDir, "Register_Empty.xlsx");
        byte[] excelContent;

        try
        {
            excelContent = File.ReadAllBytes(testFilePath);
        }
        catch (Exception ex)
        {
            Assert.Fail($"Test file not found at {testFilePath}: {ex.Message}");
            return;
        }

        // Set up the mock processing service to throw InvalidOperationException for empty files
        _mockProcessingService.Setup(x => x.UploadRegisterFromExcelAsync(
            It.IsAny<int>(), 
            It.IsAny<byte[]>(), 
            It.IsAny<string>(), 
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Excel file is empty"));

        var mockFile = CreateMockFile("Register_Empty.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", excelContent);
        var result = await _controller.UploadRegister(mockFile.Object);

        Assert.That(result, Is.TypeOf<ObjectResult>());
        var objResult = result as ObjectResult;
        Assert.That(objResult!.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));

        var errorMessage = objResult.Value as ErrMessage;
        Assert.That(errorMessage, Is.Not.Null);
        Assert.That(errorMessage!.Msg, Does.Contain("Пустой файл реестра"));

        // Verify no register was created in the database
        var registersCount = await _dbContext.Registers.CountAsync();
        Assert.That(registersCount, Is.EqualTo(0), "No register should be created for an empty Excel file");
    }

    [Test]
    public async Task UploadRegister_ReturnsBadRequest_WhenZipFileWithoutExcel()
    {
        // Arrange
        SetCurrentUserId(1); // Set to logist user

        // Load the zip file that doesn't contain any Excel files
        string emptyZipFilePath = Path.Combine(testDataDir, "Zip_Empty.zip");
        byte[] zipContent;

        try
        {
            zipContent = File.ReadAllBytes(emptyZipFilePath);
        }
        catch (Exception ex)
        {
            Assert.Fail($"Empty ZIP test file not found at {emptyZipFilePath}: {ex.Message}");
            return;
        }

        // Create a mock file with the zip content
        var mockFile = CreateMockFile("Zip_Empty.zip", "application/zip", zipContent);

        // Act
        var result = await _controller.UploadRegister(mockFile.Object);

        // Assert
        Assert.That(result, Is.TypeOf<ObjectResult>());
        var objResult = result as ObjectResult;
        Assert.That(objResult!.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));

        var errorMessage = objResult.Value as ErrMessage;
        Assert.That(errorMessage, Is.Not.Null);
        Assert.That(errorMessage!.Msg, Does.Contain("Файл реестра не найден в архиве"));

        // Verify no register was created in the database
        var registersCount = await _dbContext.Registers.CountAsync();
        Assert.That(registersCount, Is.EqualTo(0), "No register should be created when zip file contains no Excel files");
    }


    [Test]
    public async Task UploadRegister_ReturnsBadRequest_WhenTextFileUploaded()
    {
        // Arrange
        SetCurrentUserId(1); // Logist user

        // Create or load a text file for testing
        string textFilePath = Path.Combine(testDataDir, "file.txt");
        byte[] textContent;

        try
        {
            // Read the existing file.txt
            textContent = File.ReadAllBytes(textFilePath);
        }
        catch (Exception ex)
        {
            Assert.Fail($"Test file not found at {textFilePath}: {ex.Message}");
            return;
        }

        var mockFile = CreateMockFile("file.txt", "text/plain", textContent);

        // Act
        var result = await _controller.UploadRegister(mockFile.Object);

        // Assert
        Assert.That(result, Is.TypeOf<ObjectResult>());
        var objResult = result as ObjectResult;
        Assert.That(objResult!.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));

        var errorMessage = objResult.Value as ErrMessage;
        Assert.That(errorMessage, Is.Not.Null);
        Assert.That(errorMessage!.Msg, Does.Contain("Файлы формата .txt не поддерживаются"));
        Assert.That(errorMessage!.Msg, Does.Contain("Можно загрузить .xlsx, .xls, .zip, .rar"));

        // Verify no register was created in the database
        var registersCount = await _dbContext.Registers.CountAsync();
        Assert.That(registersCount, Is.EqualTo(0), "No register should be created for unsupported file types");
    }

    [Test]
    public async Task UploadRegister_ReturnsBadRequest_WhenNullFileUploaded()
    {
        SetCurrentUserId(1); // Logist user

        var result = await _controller.UploadRegister(null!);

        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
        var error = obj.Value as ErrMessage;
        Assert.That(error!.Msg, Does.Contain("Пустой файл реестра"));

        // Verify no register was created in the database
        var registersCount = await _dbContext.Registers.CountAsync();
        Assert.That(registersCount, Is.EqualTo(0), "No register should be created for null file");
    }

    [Test]
    public async Task UploadRegister_ReturnsBadRequest_WhenInvalidCompanyIdProvided()
    {
        SetCurrentUserId(1); // Logist user

        byte[] testContent = System.Text.Encoding.UTF8.GetBytes("test content");
        var mockFile = CreateMockFile("test.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", testContent);

        // Test with an invalid company ID (not WBR or Ozon)
        var result = await _controller.UploadRegister(mockFile.Object, companyId: 999);

        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
        var error = obj.Value as ErrMessage;
        Assert.That(error!.Msg, Does.Contain("Неизвестный идентификатор компании [id=999]"));

        // Verify no register was created in the database
        var registersCount = await _dbContext.Registers.CountAsync();
        Assert.That(registersCount, Is.EqualTo(0), "No register should be created for invalid company ID");
    }

    [Test]
    public async Task UploadRegister_DefaultsToWBRCompany_WhenNoCompanyIdProvided()
    {
        SetCurrentUserId(1); // Logist user

        var expectedReference = new Reference { Id = 125 };
        _mockProcessingService.Setup(x => x.UploadRegisterFromExcelAsync(
            2, // Should default to WBR ID
            It.IsAny<byte[]>(), 
            It.IsAny<string>(), 
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedReference);

        byte[] testContent = System.Text.Encoding.UTF8.GetBytes("test excel content");
        var mockFile = CreateMockFile("test.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", testContent);

        var result = await _controller.UploadRegister(mockFile.Object); // No companyId provided

        Assert.That(result, Is.TypeOf<CreatedAtActionResult>());
        var createdResult = result as CreatedAtActionResult;
        var returnedReference = createdResult!.Value as Reference;
        Assert.That(returnedReference!.Id, Is.EqualTo(expectedReference.Id));

        // Verify that the processing service was called with WBR ID (2)
        _mockProcessingService.Verify(x => x.UploadRegisterFromExcelAsync(
            2, 
            It.IsAny<byte[]>(), 
            It.IsAny<string>(), 
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task UploadRegister_Returns500InternalServerError_WhenMappingFileNotFound()
    {
        SetCurrentUserId(1); // Logist user

        byte[] testContent = System.Text.Encoding.UTF8.GetBytes("test excel content");
        var mockFile = CreateMockFile("test.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", testContent);

        // Setup mock processing service to throw FileNotFoundException
        _mockProcessingService.Setup(x => x.UploadRegisterFromExcelAsync(
            It.IsAny<int>(), 
            It.IsAny<byte[]>(), 
            It.IsAny<string>(), 
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new FileNotFoundException("Mapping file not found", "wbr_register_mapping.yaml"));

        var result = await _controller.UploadRegister(mockFile.Object);

        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status500InternalServerError));
        var error = obj.Value as ErrMessage;
        Assert.That(error!.Msg, Does.Contain("Не найдена спецификация файла реестра"));
        Assert.That(error!.Msg, Does.Contain("wbr_register_mapping.yaml"));

        // Verify no register was created in the database
        var registersCount = await _dbContext.Registers.CountAsync();
        Assert.That(registersCount, Is.EqualTo(0), "No register should be created when mapping file is not found");
    }

    [Test]
    public async Task UploadRegister_Returns500InternalServerError_WhenMappingFileNotFound_ForZipFile()
    {
        SetCurrentUserId(1); // Logist user

        // Create a real ZIP in memory with an Excel file
        using var zipStream = new MemoryStream();
        using (var archive = new System.IO.Compression.ZipArchive(zipStream, System.IO.Compression.ZipArchiveMode.Create, true))
        {
            var entry = archive.CreateEntry("test.xlsx");
            using var entryStream = entry.Open();
            byte[] excelContent = System.Text.Encoding.UTF8.GetBytes("fake excel content");
            entryStream.Write(excelContent, 0, excelContent.Length);
        }

        var mockFile = CreateMockFile("test.zip", "application/zip", zipStream.ToArray());

        // Setup mock processing service to throw FileNotFoundException
        _mockProcessingService.Setup(x => x.UploadRegisterFromExcelAsync(
            It.IsAny<int>(), 
            It.IsAny<byte[]>(), 
            It.IsAny<string>(), 
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new FileNotFoundException("Mapping file not found", "ozon_register_mapping.yaml"));

        var result = await _controller.UploadRegister(mockFile.Object);

        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status500InternalServerError));
        var error = obj.Value as ErrMessage;
        Assert.That(error!.Msg, Does.Contain("Не найдена спецификация файла реестра"));
        Assert.That(error!.Msg, Does.Contain("ozon_register_mapping.yaml"));

        // Verify no register was created in the database
        var registersCount = await _dbContext.Registers.CountAsync();
        Assert.That(registersCount, Is.EqualTo(0), "No register should be created when mapping file is not found");
    }

    [Test]
    public async Task UploadRegister_Returns400BadRequest_WhenEmptyExcelInZip()
    {
        SetCurrentUserId(1); // Logist user

        // Create a real ZIP in memory with an Excel file
        using var zipStream = new MemoryStream();
        using (var archive = new System.IO.Compression.ZipArchive(zipStream, System.IO.Compression.ZipArchiveMode.Create, true))
        {
            var entry = archive.CreateEntry("empty.xlsx");
            using var entryStream = entry.Open();
            byte[] emptyContent = [];
            entryStream.Write(emptyContent, 0, emptyContent.Length);
        }

        var mockFile = CreateMockFile("empty.zip", "application/zip", zipStream.ToArray());

        // Setup mock processing service to throw InvalidOperationException for empty Excel
        _mockProcessingService.Setup(x => x.UploadRegisterFromExcelAsync(
            It.IsAny<int>(), 
            It.IsAny<byte[]>(), 
            It.IsAny<string>(), 
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Excel file is empty"));

        var result = await _controller.UploadRegister(mockFile.Object);

        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
        var error = obj.Value as ErrMessage;
        Assert.That(error!.Msg, Does.Contain("Пустой файл реестра"));

        // Verify no register was created in the database
        var registersCount = await _dbContext.Registers.CountAsync();
        Assert.That(registersCount, Is.EqualTo(0), "No register should be created for empty Excel file in ZIP");
    }


    [TestCase(".doc", "application/msword")]
    [TestCase(".docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document")]
    [TestCase(".jpg", "image/jpeg")]
    [TestCase(".png", "image/png")]
    [TestCase(".json", "application/json")]
    [TestCase(".xml", "application/xml")]
    [TestCase(".csv", "text/csv")]
    public async Task UploadRegister_ReturnsBadRequest_ForVariousUnsupportedFileTypes(string extension, string contentType)
    {
        SetCurrentUserId(1); // Logist user

        byte[] testContent = System.Text.Encoding.UTF8.GetBytes("test content");
        var mockFile = CreateMockFile($"test{extension}", contentType, testContent);

        var result = await _controller.UploadRegister(mockFile.Object);

        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
        var error = obj.Value as ErrMessage;
        Assert.That(error!.Msg, Does.Contain($"Файлы формата {extension} не поддерживаются"));
        Assert.That(error!.Msg, Does.Contain("Можно загрузить .xlsx, .xls, .zip, .rar"));

        // Verify no register was created in the database
        var registersCount = await _dbContext.Registers.CountAsync();
        Assert.That(registersCount, Is.EqualTo(0), $"No register should be created for {extension} files");
    }

    [Test]
    public async Task UploadRegister_HandlesFileWithoutExtension()
    {
        SetCurrentUserId(1); // Logist user

        byte[] testContent = System.Text.Encoding.UTF8.GetBytes("test content");
        var mockFile = CreateMockFile("filename_without_extension", "application/octet-stream", testContent);

        var result = await _controller.UploadRegister(mockFile.Object);

        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
        var error = obj.Value as ErrMessage;
        Assert.That(error!.Msg, Does.Contain("Файлы формата  не поддерживаются"));
        Assert.That(error!.Msg, Does.Contain("Можно загрузить .xlsx, .xls, .zip, .rar"));

        // Verify no register was created in the database
        var registersCount = await _dbContext.Registers.CountAsync();
        Assert.That(registersCount, Is.EqualTo(0), "No register should be created for files without extension");
    }


    [Test]
    public async Task UploadRegister_ReturnsSuccess_ForOzonCompany()
    {
        SetCurrentUserId(1); // Logist user

        var expectedReference = new Reference { Id = 128 };
        _mockProcessingService.Setup(x => x.UploadRegisterFromExcelAsync(
            1, // Ozon ID
            It.IsAny<byte[]>(), 
            It.IsAny<string>(), 
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedReference);

        byte[] testContent = System.Text.Encoding.UTF8.GetBytes("test excel content");
        var mockFile = CreateMockFile("ozon_test.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", testContent);

        var result = await _controller.UploadRegister(mockFile.Object, companyId: 1);

        Assert.That(result, Is.TypeOf<CreatedAtActionResult>());
        var createdResult = result as CreatedAtActionResult;
        var returnedReference = createdResult!.Value as Reference;
        Assert.That(returnedReference!.Id, Is.EqualTo(expectedReference.Id));

        // Verify that the processing service was called with Ozon ID (1)
        _mockProcessingService.Verify(x => x.UploadRegisterFromExcelAsync(
            1, 
            It.IsAny<byte[]>(), 
            It.IsAny<string>(), 
            It.IsAny<CancellationToken>()), Times.Once);
    }


    [Test]
    public async Task UploadRegister_HandlesZipWithNestedDirectories()
    {
        SetCurrentUserId(1); // Logist user

        // Create a ZIP with Excel file in a nested directory
        using var zipStream = new MemoryStream();
        using (var archive = new System.IO.Compression.ZipArchive(zipStream, System.IO.Compression.ZipArchiveMode.Create, true))
        {
            // Add directory entry (some tools create these)
            archive.CreateEntry("documents/");
            
            // Add Excel file in nested directory
            var entry = archive.CreateEntry("documents/register.xlsx");
            using var entryStream = entry.Open();
            byte[] excelContent = System.Text.Encoding.UTF8.GetBytes("nested excel content");
            entryStream.Write(excelContent, 0, excelContent.Length);
        }

        var expectedReference = new Reference { Id = 130 };
        _mockProcessingService.Setup(x => x.UploadRegisterFromExcelAsync(
            It.IsAny<int>(), 
            It.IsAny<byte[]>(), 
            "documents/register.xlsx",
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedReference);

        var mockFile = CreateMockFile("nested.zip", "application/zip", zipStream.ToArray());

        var result = await _controller.UploadRegister(mockFile.Object);

        Assert.That(result, Is.TypeOf<CreatedAtActionResult>());
        var createdResult = result as CreatedAtActionResult;
        var returnedReference = createdResult!.Value as Reference;
        Assert.That(returnedReference!.Id, Is.EqualTo(expectedReference.Id));
    }

    [Test]
    public async Task UploadRegister_HandlesLargeFileUpload()
    {
        SetCurrentUserId(1); // Logist user

        // Create a large mock file (simulate what might happen with large uploads)
        byte[] largeContent = new byte[10 * 1024 * 1024]; // 10MB
        new Random().NextBytes(largeContent);

        var expectedReference = new Reference { Id = 131 };
        _mockProcessingService.Setup(x => x.UploadRegisterFromExcelAsync(
            It.IsAny<int>(), 
            It.IsAny<byte[]>(), 
            It.IsAny<string>(), 
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedReference);

        var mockFile = CreateMockFile("large.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", largeContent);

        var result = await _controller.UploadRegister(mockFile.Object);

        Assert.That(result, Is.TypeOf<CreatedAtActionResult>());
        var createdResult = result as CreatedAtActionResult;
        var returnedReference = createdResult!.Value as Reference;
        Assert.That(returnedReference!.Id, Is.EqualTo(expectedReference.Id));
    }

    [Test]
    public async Task UploadRegister_HandlesConcurrentUploads()
    {
        SetCurrentUserId(1); // Logist user

        var expectedReference1 = new Reference { Id = 132 };
        var expectedReference2 = new Reference { Id = 133 };

        // Setup different responses for different calls
        var setupSequence = _mockProcessingService.SetupSequence(x => x.UploadRegisterFromExcelAsync(
            It.IsAny<int>(), 
            It.IsAny<byte[]>(), 
            It.IsAny<string>(), 
            It.IsAny<CancellationToken>()));
        
        setupSequence.ReturnsAsync(expectedReference1);
        setupSequence.ReturnsAsync(expectedReference2);

        byte[] content1 = System.Text.Encoding.UTF8.GetBytes("first file content");
        byte[] content2 = System.Text.Encoding.UTF8.GetBytes("second file content");

        var mockFile1 = CreateMockFile("file1.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", content1);
        var mockFile2 = CreateMockFile("file2.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", content2);

        // Execute concurrent uploads
        var task1 = _controller.UploadRegister(mockFile1.Object);
        var task2 = _controller.UploadRegister(mockFile2.Object);

        var results = await Task.WhenAll(task1, task2);

        // Both should succeed
        Assert.That(results[0], Is.TypeOf<CreatedAtActionResult>());
        Assert.That(results[1], Is.TypeOf<CreatedAtActionResult>());

        var result1 = results[0] as CreatedAtActionResult;
        var result2 = results[1] as CreatedAtActionResult;

        var ref1 = result1!.Value as Reference;
        var ref2 = result2!.Value as Reference;

        Assert.That(ref1!.Id, Is.EqualTo(expectedReference1.Id));
        Assert.That(ref2!.Id, Is.EqualTo(expectedReference2.Id));
    }


    [Test]
    public async Task UploadRegister_LogsDebugInformation()
    {
        SetCurrentUserId(1); // Logist user

        var expectedReference = new Reference { Id = 134 };
        _mockProcessingService.Setup(x => x.UploadRegisterFromExcelAsync(
            It.IsAny<int>(), 
            It.IsAny<byte[]>(), 
            It.IsAny<string>(), 
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedReference);

        byte[] testContent = System.Text.Encoding.UTF8.GetBytes("test excel content");
        var mockFile = CreateMockFile("test.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", testContent);

        var result = await _controller.UploadRegister(mockFile.Object);

        Assert.That(result, Is.TypeOf<CreatedAtActionResult>());

        // Note: In a real scenario, you might want to verify that logging actually occurred
        // This would require setting up a mock logger and verifying log calls
        // For now, we just verify the method executed successfully
    }

    [Test]
    public async Task ValidateRegister_RunsService_ForLogist()
    {
        SetCurrentUserId(1);
        _dbContext.Registers.Add(new Register { Id = 5, FileName = "r.xlsx", TheOtherCompanyId = 3 });
        await _dbContext.SaveChangesAsync();

        var handle = Guid.NewGuid();
        _mockRegValidationService.Setup(s => s.StartValidationAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(handle);

        var result = await _controller.ValidateRegister(5);

        _mockRegValidationService.Verify(s => s.StartValidationAsync(5, It.IsAny<CancellationToken>()), Times.Once);
        Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
        var ok = result.Result as OkObjectResult;
        Assert.That(((GuidReference)ok!.Value!).Id, Is.EqualTo(handle));
    }

    [Test]
    public async Task ValidateRegister_ReturnsForbidden_ForNonLogist()
    {
        SetCurrentUserId(2);
        var result = await _controller.ValidateRegister(1);

        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
        _mockRegValidationService.Verify(s => s.StartValidationAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task ValidateRegister_ReturnsNotFound_WhenMissing()
    {
        SetCurrentUserId(1);
        var result = await _controller.ValidateRegister(99);

        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
    }

    [Test]
    public async Task GetValidationProgress_ReturnsData()
    {
        SetCurrentUserId(1);
        var progress = new ValidationProgress { HandleId = Guid.NewGuid(), Total = 10, Processed = 5 };
        _mockRegValidationService.Setup(s => s.GetProgress(progress.HandleId)).Returns(progress);

        var result = await _controller.GetValidationProgress(progress.HandleId);

        Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
        var ok = result.Result as OkObjectResult;
        Assert.That(ok!.Value, Is.EqualTo(progress));
    }

    [Test]
    public async Task GetValidationProgress_ReturnsNotFound()
    {
        SetCurrentUserId(1);
        var result = await _controller.GetValidationProgress(Guid.NewGuid());

        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
    }

    [Test]
    public async Task CancelValidation_ReturnsNoContent()
    {
        SetCurrentUserId(1);
        var handle = Guid.NewGuid();
        _mockRegValidationService.Setup(s => s.CancelValidation(handle)).Returns(true);

        var result = await _controller.CancelValidation(handle);

        Assert.That(result, Is.TypeOf<NoContentResult>());
    }

    [Test]
    public async Task CancelValidation_ReturnsNotFound()
    {
        SetCurrentUserId(1);
        var result = await _controller.CancelValidation(Guid.NewGuid());

        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
    }

    [Test]
    public async Task GetValidationProgress_ReturnsForbidden_ForNonLogist()
    {
        SetCurrentUserId(2); // Admin user, not logist
        var handle = Guid.NewGuid();
        var result = await _controller.GetValidationProgress(handle);

        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
        _mockRegValidationService.Verify(s => s.GetProgress(It.IsAny<Guid>()), Times.Never);
    }

    [Test]
    public async Task CancelValidation_ReturnsForbidden_ForNonLogist()
    {
        SetCurrentUserId(2); // Admin user, not logist
        var handle = Guid.NewGuid();
        var result = await _controller.CancelValidation(handle);

        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
        _mockRegValidationService.Verify(s => s.CancelValidation(It.IsAny<Guid>()), Times.Never);
    }

    [Test]
    public async Task ValidateRegister_WithRealService_CreatesFeacnLinks()
    {
        SetCurrentUserId(1);
        var register = new Register { Id = 200, FileName = "r.xlsx", TheOtherCompanyId = 3 };
        var feacnOrder = new FeacnOrder { Id = 300, Title = "t" };
        var prefix = new FeacnPrefix { Id = 400, Code = "12", FeacnOrderId = 300, FeacnOrder = feacnOrder };
        var order = new WbrOrder { Id = 201, RegisterId = 200, StatusId = 1, TnVed = "1234567890" };
        _dbContext.Registers.Add(register);
        _dbContext.FeacnOrders.Add(feacnOrder);
        _dbContext.FeacnPrefixes.Add(prefix);
        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync();

        var orderValidationService = new OrderValidationService(_dbContext, new MorphologySearchService(), new FeacnPrefixCheckService(_dbContext));
        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        var spMock = new Mock<IServiceProvider>();
        spMock.Setup(x => x.GetService(typeof(AppDbContext))).Returns(_dbContext);
        spMock.Setup(x => x.GetService(typeof(IOrderValidationService))).Returns(orderValidationService);
        spMock.Setup(x => x.GetService(typeof(IFeacnPrefixCheckService))).Returns(new FeacnPrefixCheckService(_dbContext));
        var scopeMock = new Mock<IServiceScope>();
        scopeMock.Setup(s => s.ServiceProvider).Returns(spMock.Object);
        scopeFactoryMock.Setup(f => f.CreateScope()).Returns(scopeMock.Object);

        // Update the logger type to match the expected type for RegisterValidationService
        var realRegSvc = new RegisterValidationService(_dbContext, scopeFactoryMock.Object, new LoggerFactory().CreateLogger<RegisterValidationService>(), new MorphologySearchService(), new FeacnPrefixCheckService(_dbContext));
        _controller = new RegistersController(_mockHttpContextAccessor.Object, _dbContext, _userService, _logger, realRegSvc, _mockProcessingService.Object, _mockIndPostGenerator.Object);

        var result = await _controller.ValidateRegister(200);
        var handle = ((GuidReference)((OkObjectResult)result.Result!).Value!).Id;

        // wait for completion
        ValidationProgress? progress = null;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.Elapsed < TimeSpan.FromSeconds(2))
        {
            progress = realRegSvc.GetProgress(handle);
            if (progress != null && progress.Finished)
                break;
            await Task.Delay(50);
        }

        var orderReloaded = await _dbContext.Orders.Include(o => o.BaseOrderFeacnPrefixes).FirstAsync(o => o.Id == 201);
        Assert.That(orderReloaded.BaseOrderFeacnPrefixes.Any(l => l.FeacnPrefixId == 400), Is.True);
    }

    [Test]
    public async Task NextParcel_ReturnsNextParcel_AfterGiven()
    {
        SetCurrentUserId(1);
        _dbContext.CheckStatuses.AddRange(
            new ParcelCheckStatus { Id = 101, Title = "Has" },
            new ParcelCheckStatus { Id = 201, Title = "Ok" });
        var reg = new Register { Id = 1, FileName = "r.xlsx", CompanyId = 2, TheOtherCompanyId = 3 };
        _dbContext.Registers.Add(reg);
        _dbContext.Orders.AddRange(
            new WbrOrder { Id = 10, RegisterId = 1, StatusId = 1, CheckStatusId = 101 },
            new WbrOrder { Id = 20, RegisterId = 1, StatusId = 1, CheckStatusId = 101 },
            new WbrOrder { Id = 30, RegisterId = 1, StatusId = 1, CheckStatusId = 201 }
        );
        await _dbContext.SaveChangesAsync();

        var result = await _controller.NextParcel(10);

        Assert.That(result.Value, Is.Not.Null);
        Assert.That(result.Value!.Id, Is.EqualTo(20));
    }

    [Test]
    public async Task NextParcel_PerformsCircularSearch()
    {
        SetCurrentUserId(1);
        _dbContext.CheckStatuses.Add(new ParcelCheckStatus { Id = 101, Title = "Has" });
        var reg = new Register { Id = 1, FileName = "r.xlsx", CompanyId = 1, TheOtherCompanyId = 3 }; // Ozon company
        _dbContext.Registers.Add(reg);
        var ozonOrder1 = new OzonOrder { Id = 1, RegisterId = 1, StatusId = 1, CheckStatusId = 101 };
        var ozonOrder2 = new OzonOrder { Id = 2, RegisterId = 1, StatusId = 1, CheckStatusId = 201 };
        var ozonOrder3 = new OzonOrder { Id = 3, RegisterId = 1, StatusId = 1, CheckStatusId = 101 };
        _dbContext.Orders.AddRange(ozonOrder1, ozonOrder2, ozonOrder3);
        _dbContext.OzonOrders.AddRange(ozonOrder1, ozonOrder2, ozonOrder3);
        await _dbContext.SaveChangesAsync();

        var result = await _controller.NextParcel(3);

        Assert.That(result.Value, Is.Not.Null);
        Assert.That(result.Value!.Id, Is.EqualTo(1));
    }

    [Test]
    public async Task NextParcel_ReturnsNoContent_WhenNoMatches()
    {
        SetCurrentUserId(1);
        _dbContext.CheckStatuses.Add(new ParcelCheckStatus { Id = 201, Title = "Ok" });
        var reg = new Register { Id = 1, FileName = "r.xlsx", CompanyId = 2 };
        _dbContext.Registers.Add(reg);
        _dbContext.Orders.Add(new WbrOrder { Id = 1, RegisterId = 1, StatusId = 1, CheckStatusId = 201 });
        await _dbContext.SaveChangesAsync();

        var result = await _controller.NextParcel(1);

        Assert.That(result.Result, Is.TypeOf<NoContentResult>());
    }

    [Test]
    public async Task NextParcel_ReturnsNotFound_WhenOrderMissing()
    {
        SetCurrentUserId(1);
        var result = await _controller.NextParcel(99);

        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
    }

    [Test]
    public async Task NextParcel_ReturnsForbidden_ForNonLogist()
    {
        SetCurrentUserId(2);
        var result = await _controller.NextParcel(1);

        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task DownloadRegister_ReturnsFile_ForLogist()
    {
        SetCurrentUserId(1);
        var register = new Register { Id = 10, FileName = "reg.xlsx", CompanyId = 2 };
        _dbContext.Registers.Add(register);
        await _dbContext.SaveChangesAsync();

        byte[] bytes = [1, 2, 3];
        _mockProcessingService.Setup(s => s.DownloadRegisterToExcelAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(bytes);

        var result = await _controller.DownloadRegister(10);

        Assert.That(result, Is.TypeOf<FileContentResult>());
        var file = result as FileContentResult;
        Assert.That(file!.FileDownloadName, Is.EqualTo("reg.xlsx"));
        Assert.That(file.ContentType, Is.EqualTo("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"));
        Assert.That(file.FileContents, Is.EqualTo(bytes));
    }

    [Test]
    public async Task DownloadRegister_ReturnsNotFound_WhenMissing()
    {
        SetCurrentUserId(1);
        var result = await _controller.DownloadRegister(99);

        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
        _mockProcessingService.Verify(s => s.DownloadRegisterToExcelAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task DownloadRegister_ReturnsForbidden_ForNonLogist()
    {
        SetCurrentUserId(2);
        var register = new Register { Id = 11, FileName = "r.xlsx", CompanyId = 2 };
        _dbContext.Registers.Add(register);
        await _dbContext.SaveChangesAsync();

        var result = await _controller.DownloadRegister(11);

        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
        _mockProcessingService.Verify(s => s.DownloadRegisterToExcelAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Generate_ReturnsFile_ForLogist()
    {
        SetCurrentUserId(1);
        var register = new Register { Id = 20, FileName = "r.xlsx", CompanyId = 2 };
        _dbContext.Registers.Add(register);
        await _dbContext.SaveChangesAsync();

        byte[] zip = [1, 2, 3, 4];
        _mockIndPostGenerator.Setup(g => g.GenerateXML4R(20)).ReturnsAsync(("IndPost_r.zip", zip));

        var result = await _controller.Generate(20);

        Assert.That(result, Is.TypeOf<FileContentResult>());
        var file = result as FileContentResult;
        Assert.That(file!.FileDownloadName, Is.EqualTo("IndPost_r.zip"));
        Assert.That(file.ContentType, Is.EqualTo("application/zip"));
        Assert.That(file.FileContents, Is.EqualTo(zip));
    }

    [Test]
    public async Task Generate_ReturnsNotFound_WhenMissing()
    {
        SetCurrentUserId(1);

        var result = await _controller.Generate(999);

        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
        _mockIndPostGenerator.Verify(g => g.GenerateXML4R(It.IsAny<int>()), Times.Never);
    }

    [Test]
    public async Task Generate_ReturnsForbidden_ForNonLogist()
    {
        SetCurrentUserId(2);
        var register = new Register { Id = 21, FileName = "r.xlsx", CompanyId = 2 };
        _dbContext.Registers.Add(register);
        await _dbContext.SaveChangesAsync();

        var result = await _controller.Generate(21);

        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
        _mockIndPostGenerator.Verify(g => g.GenerateXML4R(It.IsAny<int>()), Times.Never);
    }

    [Test]
    public async Task NextParcel_SkipsMarkedByPartnerOrders()
    {
        SetCurrentUserId(1);
        _dbContext.CheckStatuses.AddRange(
            new ParcelCheckStatus { Id = 101, Title = "Has" },
            new ParcelCheckStatus { Id = 201, Title = "Ok" },
            new ParcelCheckStatus { Id = 200, Title = "MarkedByPartner" }
        );
        var reg = new Register { Id = 1, FileName = "r.xlsx", CompanyId = 2, TheOtherCompanyId = 3 };
        _dbContext.Registers.Add(reg);
        _dbContext.Orders.AddRange(
            new WbrOrder { Id = 10, RegisterId = 1, StatusId = 1, CheckStatusId = 101 },
            new WbrOrder { Id = 20, RegisterId = 1, StatusId = 1, CheckStatusId = 200 }, // MarkedByPartner
            new WbrOrder { Id = 30, RegisterId = 1, StatusId = 1, CheckStatusId = 101 }
        );
        await _dbContext.SaveChangesAsync();

        var result = await _controller.NextParcel(10);
        Assert.That(result.Value, Is.Not.Null);
        Assert.That(result.Value!.Id, Is.EqualTo(30)); // Should skip 20
    }

    [Test]
    public async Task SetParcelStatuses_DoesNotUpdateMarkedByPartnerOrders()
    {
        SetCurrentUserId(1);
        _dbContext.CheckStatuses.AddRange(
            new ParcelCheckStatus { Id = 101, Title = "Has" },
            new ParcelCheckStatus { Id = 200, Title = "MarkedByPartner" }
        );
        _dbContext.Statuses.Add(new ParcelStatus { Id = 99, Title = "TestStatus" });
        var reg = new Register { Id = 2, FileName = "r.xlsx", CompanyId = 2, TheOtherCompanyId = 3 };
        _dbContext.Registers.Add(reg);
        _dbContext.Orders.AddRange(
            new WbrOrder { Id = 1, RegisterId = 2, StatusId = 1, CheckStatusId = 101 },
            new WbrOrder { Id = 2, RegisterId = 2, StatusId = 1, CheckStatusId = 200 }
        );
        await _dbContext.SaveChangesAsync();

        await _controller.SetParcelStatuses(2, 99);
        var order1 = await _dbContext.Orders.FindAsync(1);
        var order2 = await _dbContext.Orders.FindAsync(2);
        Assert.That(order1!.StatusId, Is.EqualTo(99)); // Updated
        Assert.That(order2!.StatusId, Is.EqualTo(1));  // Not updated
    }
}

