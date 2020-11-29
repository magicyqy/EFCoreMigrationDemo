using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace EFCoreMigrationDemo.Model
{
    [DisplayName("用户表")]
    public class User
    {
        public int Id { get; set; }
        [DisplayName("手机号")]
        public virtual string Mobile { get; set; }
        [DisplayName("状态")]
        public virtual int State { get; set; }

        [DisplayName("代号")]
        public virtual string Code { get; set; }

        [DisplayName("名称")]
        public virtual string Name { get; set; }
        [DisplayName("密码")]
        public virtual string PassWord { get; set; }
        [DisplayName("邮件")]
        public virtual string Email { get; set; }


        public virtual IList<UserRole> UserRoles { get; set; }
       
      

    }
}
