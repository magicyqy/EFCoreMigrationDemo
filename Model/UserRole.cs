using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EFCoreMigrationDemo.Model
{
    public class UserRole
    {
        [Key, Column(Order = 1)]
        public virtual int UserId { get; set; }
        public virtual User User { get; set; }
        [Key, Column(Order = 2)]
        public virtual int RoleId { get; set; }
        public virtual Role Role { get; set; }
    }
}