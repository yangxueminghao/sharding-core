using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShardingCore.Test6x.Domain.Entities;

namespace ShardingCore.Test6x.Domain.Maps
{
/*
* @Author: xjm
* @Description:
* @Date: Thursday, 14 January 2021 15:37:33
* @Email: 326308290@qq.com
*/
    public class SysUserModMap:IEntityTypeConfiguration<SysUserMod>
    {
        public void Configure(EntityTypeBuilder<SysUserMod> builder)
        {
            builder.HasKey(o => o.Id);
            builder.Property(o => o.Id).IsRequired().HasMaxLength(128);
            builder.Property(o => o.Name).HasMaxLength(128);
            builder.ToTable(nameof(SysUserMod));
        }
    }
}