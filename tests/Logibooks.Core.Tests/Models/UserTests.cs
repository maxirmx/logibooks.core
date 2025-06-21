using NUnit.Framework;
using Logibooks.Core.Models;

namespace Logibooks.Core.Tests.Models;

public class UserTests
{
    [Test]
    public void HasAnyRole_ReturnsFalse_WhenNoRoles()
    {
        var user = new User();
        Assert.False(user.HasAnyRole());
    }

    [Test]
    public void HasAnyRole_ReturnsTrue_WhenRolesExist()
    {
        var role = new Role { Id = 1, Name = "admin", Title = "Admin" };
        var user = new User { UserRoles = new List<UserRole> { new UserRole { Role = role } } };
        Assert.True(user.HasAnyRole());
    }

    [Test]
    public void HasRole_IgnoresCase()
    {
        var role = new Role { Id = 1, Name = "Admin", Title = "Admin" };
        var user = new User { UserRoles = new List<UserRole> { new UserRole { Role = role } } };
        Assert.True(user.HasRole("admin"));
    }

    [Test]
    public void HasRole_ReturnsFalse_WhenRoleMissing()
    {
        var user = new User();
        Assert.False(user.HasRole("admin"));
    }

    [Test]
    public void IsAdministrator_ReturnsTrue_WhenAdminRolePresent()
    {
        var role = new Role { Id = 2, Name = "administrator", Title = "Administrator" };
        var user = new User { UserRoles = new List<UserRole> { new UserRole { Role = role } } };
        Assert.True(user.IsAdministrator());
    }
}
