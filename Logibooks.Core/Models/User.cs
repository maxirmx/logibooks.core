
using System.ComponentModel.DataAnnotations.Schema;

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
    }
}
