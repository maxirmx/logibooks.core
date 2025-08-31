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
using NUnit.Framework;
using System.Threading.Tasks;
using Logibooks.Core.Models;
using Logibooks.Core.RestModels;

namespace Logibooks.Core.Tests.Controllers.Registers;

[TestFixture]
public class RegistersControllerTheNextParcelTests : RegistersControllerTestsBase
{
    [Test]
    public async Task TheNextParcel_ReturnsNextParcel_ForWbrCompany()
    {
        // Arrange
        SetCurrentUserId(1); // Logist user
        
        var register = new Register { Id = 1, FileName = "test.xlsx", CompanyId = 2, TheOtherCompanyId = 3 }; // WBR company
        _dbContext.Registers.Add(register);

        var wbrParcel1 = new WbrParcel { Id = 10, RegisterId = 1, StatusId = 1, CheckStatusId = 1 };
        var wbrParcel2 = new WbrParcel { Id = 20, RegisterId = 1, StatusId = 1, CheckStatusId = 1 };
        var wbrParcel3 = new WbrParcel { Id = 30, RegisterId = 1, StatusId = 1, CheckStatusId = 1 };
        
        _dbContext.Parcels.AddRange(wbrParcel1, wbrParcel2, wbrParcel3);
        _dbContext.WbrParcels.AddRange(wbrParcel1, wbrParcel2, wbrParcel3);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _controller.TheNextParcel(10);

        // Assert
        Assert.That(result.Value, Is.Not.Null, "Should return the next parcel");
        Assert.That(result.Value!.Id, Is.EqualTo(20), "Should return parcel with ID 20 (next after 10)");
    }

    [Test]
    public async Task TheNextParcel_ReturnsNextParcel_ForOzonCompany()
    {
        // Arrange
        SetCurrentUserId(1); // Logist user
        
        var register = new Register { Id = 1, FileName = "test.xlsx", CompanyId = 1, TheOtherCompanyId = 3 }; // Ozon company
        _dbContext.Registers.Add(register);

        var ozonParcel1 = new OzonParcel { Id = 15, RegisterId = 1, StatusId = 1, CheckStatusId = 1 };
        var ozonParcel2 = new OzonParcel { Id = 25, RegisterId = 1, StatusId = 1, CheckStatusId = 1 };
        var ozonParcel3 = new OzonParcel { Id = 35, RegisterId = 1, StatusId = 1, CheckStatusId = 1 };
        
        _dbContext.Parcels.AddRange(ozonParcel1, ozonParcel2, ozonParcel3);
        _dbContext.OzonParcels.AddRange(ozonParcel1, ozonParcel2, ozonParcel3);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _controller.TheNextParcel(15);

        // Assert
        Assert.That(result.Value, Is.Not.Null, "Should return the next parcel");
        Assert.That(result.Value!.Id, Is.EqualTo(25), "Should return parcel with ID 25 (next after 15)");
    }


    [Test]
    public async Task TheNextParcel_ReturnsNoContent_WhenCurrentParcelIsLast()
    {
        // Arrange
        SetCurrentUserId(1); // Logist user
        
        var register = new Register { Id = 1, FileName = "test.xlsx", CompanyId = 2, TheOtherCompanyId = 3 }; // WBR company
        _dbContext.Registers.Add(register);

        var wbrParcel1 = new WbrParcel { Id = 10, RegisterId = 1, StatusId = 1, CheckStatusId = 1 };
        var wbrParcel2 = new WbrParcel { Id = 20, RegisterId = 1, StatusId = 1, CheckStatusId = 1 };
        var wbrParcel3 = new WbrParcel { Id = 30, RegisterId = 1, StatusId = 1, CheckStatusId = 1 };
        
        _dbContext.Parcels.AddRange(wbrParcel1, wbrParcel2, wbrParcel3);
        _dbContext.WbrParcels.AddRange(wbrParcel1, wbrParcel2, wbrParcel3);
        await _dbContext.SaveChangesAsync();

        // Act - Request next parcel after the last one (ID 30)
        var result = await _controller.TheNextParcel(30);

        // Assert
        Assert.That(result.Result, Is.TypeOf<NoContentResult>(), "Should return NoContent when current parcel is the last one");
    }

    [Test]
    public async Task TheNextParcel_ReturnsNotFound_WhenParcelDoesNotExist()
    {
        // Arrange
        SetCurrentUserId(1); // Logist user

        // Act
        var result = await _controller.TheNextParcel(999);

        // Assert
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var objectResult = result.Result as ObjectResult;
        Assert.That(objectResult!.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound), "Should return 404 when parcel doesn't exist");
    }

    [Test]
    public async Task TheNextParcel_ReturnsForbidden_ForNonLogist()
    {
        // Arrange
        SetCurrentUserId(2); // Admin user (non-logist)
        
        var register = new Register { Id = 1, FileName = "test.xlsx", CompanyId = 2, TheOtherCompanyId = 3 };
        _dbContext.Registers.Add(register);

        var wbrParcel = new WbrParcel { Id = 10, RegisterId = 1, StatusId = 1, CheckStatusId = 1 };
        _dbContext.Parcels.Add(wbrParcel);
        _dbContext.WbrParcels.Add(wbrParcel);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _controller.TheNextParcel(10);

        // Assert
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var objectResult = result.Result as ObjectResult;
        Assert.That(objectResult!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden), "Should return 403 for non-logist users");
    }

    [Test]
    public async Task TheNextParcel_ReturnsNoContent_WhenDerivedParcelNotFound()
    {
        // Arrange
        SetCurrentUserId(1); // Logist user
        
        var register = new Register { Id = 1, FileName = "test.xlsx", CompanyId = 2, TheOtherCompanyId = 3 }; // WBR company
        _dbContext.Registers.Add(register);

        var wbrParcel1 = new WbrParcel { Id = 10, RegisterId = 1, StatusId = 1, CheckStatusId = 1 };
        var wbrParcel2 = new WbrParcel { Id = 20, RegisterId = 1, StatusId = 1, CheckStatusId = 1 };
        
        // Add both parcels to base Orders table and WbrOrders table
        _dbContext.Parcels.AddRange(wbrParcel1, wbrParcel2);
        _dbContext.WbrParcels.AddRange(wbrParcel1, wbrParcel2);
        await _dbContext.SaveChangesAsync();

        // Now manually remove parcel 20 from WbrOrders to simulate derived parcel not found
        _dbContext.WbrParcels.Remove(wbrParcel2);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _controller.TheNextParcel(10);

        // Assert
        Assert.That(result.Result, Is.TypeOf<NoContentResult>(), "Should return NoContent when derived parcel is not found");
    }


    [Test]
    public async Task TheNextParcel_HandlesEdgeCaseWithSingleParcel()
    {
        // Arrange
        SetCurrentUserId(1); // Logist user
        
        var register = new Register { Id = 1, FileName = "test.xlsx", CompanyId = 1, TheOtherCompanyId = 3 }; // Ozon company
        _dbContext.Registers.Add(register);

        var ozonParcel = new OzonParcel { Id = 42, RegisterId = 1, StatusId = 1, CheckStatusId = 1 };
        
        _dbContext.Parcels.Add(ozonParcel);
        _dbContext.OzonParcels.Add(ozonParcel);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _controller.TheNextParcel(42);

        // Assert
        Assert.That(result.Result, Is.TypeOf<NoContentResult>(), "Should return NoContent when there's only one parcel and it's the current one");
    }

    [Test]
    public async Task TheNextParcel_ReturnsCorrectParcelViewItem()
    {
        // Arrange
        SetCurrentUserId(1); // Logist user
        
        var register = new Register { Id = 1, FileName = "test.xlsx", CompanyId = 2, TheOtherCompanyId = 3 }; // WBR company
        _dbContext.Registers.Add(register);

        var wbrParcel1 = new WbrParcel { Id = 10, RegisterId = 1, StatusId = 5, CheckStatusId = 3 };
        var wbrParcel2 = new WbrParcel 
        { 
            Id = 20, 
            RegisterId = 1, 
            StatusId = 7, 
            CheckStatusId = 4,
            UnitPrice = 99.99m
        };
        
        _dbContext.Parcels.AddRange(wbrParcel1, wbrParcel2);
        _dbContext.WbrParcels.AddRange(wbrParcel1, wbrParcel2);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _controller.TheNextParcel(10);

        // Assert
        Assert.That(result.Value, Is.Not.Null, "Should return the next parcel");
        var parcelViewItem = result.Value!;
        Assert.That(parcelViewItem.Id, Is.EqualTo(20), "Should have correct ID");
        Assert.That(parcelViewItem.RegisterId, Is.EqualTo(1), "Should have correct RegisterId");
        Assert.That(parcelViewItem.StatusId, Is.EqualTo(7), "Should have correct StatusId");
        Assert.That(parcelViewItem.CheckStatusId, Is.EqualTo(4), "Should have correct CheckStatusId");
        Assert.That(parcelViewItem.UnitPrice, Is.EqualTo(99.99m), "Should have correct UnitPrice");
    }

}