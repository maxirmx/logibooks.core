// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application

using System.Collections.Generic;
using NUnit.Framework;

using Logibooks.Core.Models;

namespace Logibooks.Core.Tests.Models;

public class UserTests
{
    [Test]
    public void HasAnyRole_ReturnsFalse_WhenNoRoles()
    {
        var user = new User { Email = "test@example.com", Password = "password123" };
        Assert.That(user.HasAnyRole(), Is.False);
    }

    [Test]
    public void HasAnyRole_ReturnsTrue_WhenRolesExist()
    {
        var role = new Role { Id = 1, Name = "admin", Title = "Admin" };
        var user = new User
        {
            Email = "test@example.com",
            Password = "password123",
            UserRoles = new List<UserRole> { new() { Role = role } }
        };
        Assert.That(user.HasAnyRole(), Is.True);
    }

    [Test]
    public void HasRole_IgnoresCase()
    {
        var role = new Role { Id = 1, Name = "Admin", Title = "Admin" };
        var user = new User
        {
            Email = "test@example.com",
            Password = "password123",
            UserRoles = new List<UserRole> { new() { Role = role } }
        };
        Assert.That(user.HasRole("admin"), Is.True);
    }

    [Test]
    public void HasRole_ReturnsFalse_WhenRoleMissing()
    {
        var user = new User { Email = "test@example.com", Password = "password123" };
        Assert.That(user.HasRole("admin"), Is.False);
    }

    [Test]
    public void IsAdministrator_ReturnsTrue_WhenAdminRolePresent()
    {
        var role = new Role { Id = 2, Name = "administrator", Title = "Administrator" };
        var user = new User
        {
            Email = "test@example.com",
            Password = "password123",
            UserRoles = new List<UserRole> { new() { Role = role } }
        };
        Assert.That(user.IsAdministrator(), Is.True);
    }
}
