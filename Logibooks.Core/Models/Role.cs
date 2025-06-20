using System.ComponentModel.DataAnnotations.Schema;
using System.Collections.Generic;

namespace Logibooks.Core.Models
{
    [Table("roles")]
    public class Role
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("name")]
        public required string Name { get; set; }

        [Column("title")]
        public required string Title { get; set; }

        public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    }
}
