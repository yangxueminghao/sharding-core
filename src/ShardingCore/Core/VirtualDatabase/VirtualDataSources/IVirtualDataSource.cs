using System;
using System.Collections.Generic;
using ShardingCore.Core.VirtualDatabase.VirtualDataSources.PhysicDataSources;
using ShardingCore.Core.VirtualRoutes;
using ShardingCore.Core.VirtualRoutes.DataSourceRoutes;
using ShardingCore.Core.VirtualTables;

namespace ShardingCore.Core.VirtualDatabase.VirtualDataSources
{
    /*
    * @Author: xjm
    * @Description:
    * @Date: Friday, 05 February 2021 13:01:39
    * @Email: 326308290@qq.com
    */
    /// <summary>
    /// 虚拟数据源 连接所有的实际数据源
    /// </summary>
    public interface IVirtualDataSource
    {
        Type ShardingDbContextType{get; }
        string DefaultDataSourceName { get; }

        /// <summary>
        /// 路由到具体的物理数据源
        /// </summary>
        /// <returns>data source names</returns>
        List<string> RouteTo(Type entityType,ShardingDataSourceRouteConfig routeRouteConfig);

        /// <summary>
        /// 获取当前数据源的路由
        /// </summary>
        /// <returns></returns>
        IVirtualDataSourceRoute GetRoute(Type entityType);

        ISet<IPhysicDataSource> GetAllPhysicDataSources();
        IPhysicDataSource GetDefaultDataSource();
        /// <summary>
        /// 获取数据源
        /// </summary>
        /// <param name="dataSourceName"></param>
        /// <returns></returns>
        IPhysicDataSource GetPhysicDataSource(string dataSourceName);

        /// <summary>
        /// 添加物理表 add physic data source
        /// </summary>
        /// <param name="physicDataSource"></param>
        /// <returns>是否添加成功</returns>
        bool AddPhysicDataSource(IPhysicDataSource physicDataSource);

    }
}