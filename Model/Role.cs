using System.ComponentModel;
using System.Collections.Generic;
namespace EFCoreMigrationDemo.Model
{
    public class Role
    {
        public int Id { get; set; }
        [DisplayName("代码")]
        public virtual string Code { get; set; }

        [DisplayName("名称")]
        public virtual string Name { get; set; }
   

        [DisplayName("角色用户")]
        public virtual IList<UserRole> UserRoles { get; set; }
    }
}