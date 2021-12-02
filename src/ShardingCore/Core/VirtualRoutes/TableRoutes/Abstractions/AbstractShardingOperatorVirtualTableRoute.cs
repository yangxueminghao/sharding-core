using ShardingCore.Core.PhysicTables;
using ShardingCore.Exceptions;
using ShardingCore.Extensions;
using ShardingCore.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;

namespace ShardingCore.Core.VirtualRoutes.TableRoutes.Abstractions
{
    /*
    * @Author: xjm
    * @Description:
    * @Date: Saturday, 19 December 2020 19:55:24
    * @Email: 326308290@qq.com
    */
    public abstract class AbstractShardingOperatorVirtualTableRoute<TEntity, TKey> : AbstractShardingFilterVirtualTableRoute<TEntity, TKey> where TEntity : class
    {
        protected override List<IPhysicTable> DoRouteWithPredicate(List<IPhysicTable> allPhysicTables, IQueryable queryable)
        {
            //获取所有需要路由的表后缀 
            var filter = ShardingUtil.GetRouteShardingTableFilter<TKey>(queryable, EntityMetadata, GetRouteToFilter,true);
            var physicTables = allPhysicTables.Where(o => filter(o.Tail)).ToList();
            return physicTables;
        }


        /// <summary>
        /// 如何路由到具体表 shardingKeyValue:分表的值, 返回结果:如果返回true表示返回该表 第一个参数 tail 第二参数是否返回该物理表
        /// </summary>
        /// <param name="shardingKey">分表的值</param>
        /// <param name="shardingOperator">操作</param>
        /// <returns>如果返回true表示返回该表 第一个参数 tail 第二参数是否返回该物理表</returns>
        protected abstract Expression<Func<string, bool>> GetRouteToFilter(TKey shardingKey, ShardingOperatorEnum shardingOperator);

        public override IPhysicTable RouteWithValue(List<IPhysicTable> allPhysicTables, object shardingKey)
        {
            var shardingKeyToTail = ShardingKeyToTail(shardingKey);

            var physicTables = allPhysicTables.Where(o => o.Tail== shardingKeyToTail).ToList();
            if (physicTables.IsEmpty())
            {
                throw new ShardingCoreException($"sharding key route not match {EntityMetadata.EntityType} -> [{EntityMetadata.ShardingTableProperty.Name}] ->【{shardingKey}】 all tails ->[{string.Join(",", allPhysicTables.Select(o=>o.FullName))}]");
            }

            if (physicTables.Count > 1)
                throw new ShardingCoreException($"more than one route match table:{string.Join(",", physicTables.Select(o => $"[{o.FullName}]"))}");
            return physicTables[0];
        }

    }
}