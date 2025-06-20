using System.ComponentModel.DataAnnotations.Schema;
using System.Collections.Generic;
using System.Linq;

namespace Logibooks.Core.Models
{
    [Table("users")]
    public class User
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("first_name")]
        public string FirstName { get; set; } = "";

        [Column("last_name")]
        public string LastName { get; set; } = "";

        [Column("patronimic")]
        public string Patronimic { get; set; } = "";

        [Column("email")]
        public required string Email { get; set; }

        [Column("password")]
        public required string Password { get; set; }

        public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();

        public bool HasAnyRole() => UserRoles.Any();

        public bool HasRole(string roleName)
        {
            if (string.IsNullOrWhiteSpace(roleName))
            {
                return false;
            }

            return UserRoles.Any(ur => string.Equals(ur.Role?.Name, roleName, StringComparison.OrdinalIgnoreCase));
        }

        public bool IsAdministrator() => HasRole("administrator");
    }
}
